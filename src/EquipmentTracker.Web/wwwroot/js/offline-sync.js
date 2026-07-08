/**
 * offline-sync.js — Auto-sync pending offline transactions when connectivity returns.
 *
 * AC3/AC5: Listens for navigator.onLine events and triggers sync automatically.
 *          Sync is ONLY started when navigator.onLine === true AND a server probe
 *          (HEAD /api/offline/probe) returns HTTP 200, preventing premature sync
 *          on captive portal or lossy connections.
 *
 * AC6: Full offline banner shown within 3s of connectivity change (replaces dot).
 *
 * AC8: Service worker keep-alive ping sent every 25 s to prevent Android Doze
 *      from freezing the SW before Persistent Storage takes effect.
 *
 * On Chrome/Firefox: registers a Background Sync tag so the SW can retry even
 *   after the page is closed.
 * On iOS (no Background Sync API): shows a "Sync Now" button for manual trigger.
 *
 * Depends on: offline-db.js (window.OfflineDB), offline-queue.js (window.OfflineQueue)
 */

const SYNC_TAG       = 'equipment-tracker-sync';
const SYNC_ENDPOINT  = '/api/offline/sync';
const PROBE_ENDPOINT = '/api/offline/probe';   // HEAD request — AC5 server probe
const PROBE_TIMEOUT_MS = 5000;                 // 5 s — fail fast on lossy connections

// ── Background Sync capability detection ────────────────────────────────────

const supportsBackgroundSync =
    'serviceWorker' in navigator &&
    'SyncManager' in window;

// ── AC8: Service worker keep-alive ping ─────────────────────────────────────

let _keepAliveTimer = null;

function startKeepAlive() {
    if (_keepAliveTimer) return;           // already running
    if (!('serviceWorker' in navigator)) return;

    _keepAliveTimer = setInterval(async () => {
        try {
            const reg = await navigator.serviceWorker.ready;
            if (reg.active) {
                reg.active.postMessage({ type: 'SW_KEEP_ALIVE' });
            }
        } catch { /* silent — keep-alive is best-effort */ }
    }, 25000);
}

// ── AC5: Server probe before sync clock ─────────────────────────────────────

/**
 * Returns true if the server is reachable (HEAD probe returns HTTP 200).
 * Falls back to false on network error or non-2xx response.
 */
async function serverProbeOk() {
    try {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);
        const resp = await fetch(PROBE_ENDPOINT, {
            method: 'HEAD',
            cache: 'no-store',
            signal: controller.signal
        });
        clearTimeout(timeout);
        return resp.ok;          // 200–299
    } catch {
        return false;            // network error, timeout, or offline
    }
}

/**
 * Waits until navigator.onLine is true AND the server probe succeeds,
 * then calls triggerSync(). Retries every 5 s if the probe fails.
 * Maximum wait before giving up: 30 s (AC5: "within 30 seconds of connectivity").
 */
async function waitForConnectivityThenSync() {
    const RETRY_INTERVAL_MS = 5000;
    const MAX_WAIT_MS       = 30000;
    const startedAt         = Date.now();

    while (Date.now() - startedAt < MAX_WAIT_MS) {
        if (navigator.onLine && await serverProbeOk()) {
            console.info('[OfflineSync] Server probe OK — starting sync.');
            await triggerSync();
            return;
        }
        console.info('[OfflineSync] Server not reachable yet — retrying in 5 s…');
        await new Promise(resolve => setTimeout(resolve, RETRY_INTERVAL_MS));
    }
    console.warn('[OfflineSync] Server probe timed out after 30 s — sync deferred to next online event.');
}

// ── Connectivity event wiring ────────────────────────────────────────────────

window.addEventListener('online',  handleOnline);
window.addEventListener('offline', handleOffline);

async function handleOnline() {
    updateOnlineIndicator(true);
    console.info('[OfflineSync] Connection restored.');

    if (supportsBackgroundSync) {
        try {
            const reg = await navigator.serviceWorker.ready;
            await reg.sync.register(SYNC_TAG);
            console.info('[OfflineSync] Background Sync tag registered:', SYNC_TAG);
        } catch (err) {
            console.warn('[OfflineSync] Background Sync register failed, falling back to foreground sync.', err);
            await waitForConnectivityThenSync();
        }
    } else {
        // iOS or other browsers without Background Sync.
        await waitForConnectivityThenSync();
    }
}

function handleOffline() {
    updateOnlineIndicator(false);
    console.info('[OfflineSync] Connection lost. Transactions will be queued.');
    renderSyncNowButton();
}

// ── Core sync logic ──────────────────────────────────────────────────────────

/**
 * Reads all pending transactions from IndexedDB, POSTs them to /api/offline/sync,
 * and updates their status in the local store.
 * @returns {Promise<void>}
 */
async function triggerSync() {
    const pending = await window.OfflineDB.getPending();
    if (pending.length === 0) {
        console.info('[OfflineSync] No pending transactions to sync.');
        hideSyncNowButton();
        return;
    }

    console.info(`[OfflineSync] Syncing ${pending.length} pending transaction(s)…`);

    // Sort chronologically before sending.
    const ordered = [...pending].sort(
        (a, b) => new Date(a.offlineTimestamp) - new Date(b.offlineTimestamp)
    );

    let response;
    try {
        response = await fetch(SYNC_ENDPOINT, {
            method  : 'POST',
            headers : { 'Content-Type': 'application/json' },
            body    : JSON.stringify(ordered.map(tx => ({
                deviceTransactionId : tx.deviceTransactionId,
                type                : tx.type,
                itemId              : tx.itemId,
                borrowerUserId      : tx.borrowerUserId,
                offlineTimestamp    : tx.offlineTimestamp,
                deviceId            : tx.deviceId,
                conditionNotes      : tx.conditionNotes ?? null   // AC4: condition notes
            })))
        });
    } catch (networkErr) {
        console.warn('[OfflineSync] Network error during sync — will retry on next online event.', networkErr);
        return;
    }

    if (!response.ok) {
        const body = await response.text().catch(() => '');
        console.error('[OfflineSync] Sync endpoint returned', response.status, body);
        return;
    }

    const results = await response.json();
    let conflicts = 0;
    let workerMessages = [];

    for (const result of results) {
        const dbStatus = result.status === 'success'  ? 'synced'
                       : result.status === 'conflict' ? 'conflict'
                       : 'error';
        await window.OfflineDB.updateStatus(
            result.deviceTransactionId,
            dbStatus,
            result.conflictDetails ?? null
        );
        if (result.status === 'conflict') {
            conflicts++;
            // AC7: Worker feedback message for conflict
            if (result.workerMessage) {
                workerMessages.push(result.workerMessage);
            }
        }
    }

    await window.OfflineQueue.updateBadge();
    hideSyncNowButton();

    if (conflicts > 0) {
        const workerFeedback = workerMessages.length > 0
            ? ` ${workerMessages[0]}`
            : '';
        window.OfflineQueue.showAlert('warning',
            `Sync complete. ${conflicts} conflict(s) detected —${workerFeedback} ` +
            `check the <a href="/OfflineQueue">Offline Queue</a> for details.`);
    } else {
        console.info('[OfflineSync] Sync complete. All transactions processed.');
    }
}

// ── Sync Now button (iOS / manual fallback) ──────────────────────────────────

function renderSyncNowButton() {
    if (document.getElementById('offline-sync-now-btn')) return;

    const btn = document.createElement('button');
    btn.id          = 'offline-sync-now-btn';
    btn.textContent = '🔄 Sync Now';
    btn.className   = 'btn btn-sm btn-warning';
    btn.title       = 'Tap to sync pending offline transactions';
    btn.style.cssText =
        'position:fixed;bottom:16px;right:16px;z-index:9998;' +
        'box-shadow:0 2px 8px rgba(0,0,0,0.3);';
    btn.addEventListener('click', async () => {
        btn.disabled    = true;
        btn.textContent = '⏳ Syncing…';
        await triggerSync();
        btn.remove();
    });
    document.body.appendChild(btn);
}

function hideSyncNowButton() {
    document.getElementById('offline-sync-now-btn')?.remove();
}

// ── AC6: Full offline banner (replaces dot indicator) ───────────────────────

/**
 * Shows/hides the full offline banner and updates the dot indicator.
 * Banner appears within 3 s (synchronous DOM update — no timer needed).
 * Body padding-top is adjusted so navbar content is not obscured.
 */
function updateOnlineIndicator(isOnline) {
    // Dot indicator (legacy, kept for coordinator screens that reference it)
    const dot = document.getElementById('online-status-dot');
    if (dot) {
        dot.title     = isOnline ? 'Online' : 'Offline';
        dot.className = isOnline ? 'online-dot online' : 'online-dot offline';
    }

    // Full offline banner (AC6)
    const banner = document.getElementById('offline-status-banner');
    if (banner) {
        if (isOnline) {
            banner.classList.remove('banner-visible');
            document.body.classList.remove('offline-mode');
        } else {
            banner.classList.add('banner-visible');
            document.body.classList.add('offline-mode');
        }
    }
}

// ── Initialise on DOM ready ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    updateOnlineIndicator(navigator.onLine);

    // AC8: start SW keep-alive immediately
    startKeepAlive();

    if (!navigator.onLine) {
        renderSyncNowButton();
    } else {
        const count = await window.OfflineQueue.getPendingCount();
        if (count > 0) {
            console.info(`[OfflineSync] Found ${count} pending transaction(s) from previous session. Probing server…`);
            await waitForConnectivityThenSync();
        }
    }
});

// Expose for service worker message handler
window.OfflineSync = {
    triggerSync,
    serverProbeOk
};

    hideSyncNowButton();

    if (conflicts > 0) {
        window.OfflineQueue.showAlert('warning',
            `Sync complete. ${conflicts} conflict(s) detected — ` +
            `check the <a href="/OfflineQueue">Offline Queue</a> for details.`);
    } else {
        console.info('[OfflineSync] Sync complete. All transactions processed.');
    }
}

// ── Sync Now button (iOS / manual fallback) ──────────────────────────────────

function renderSyncNowButton() {
    if (document.getElementById('offline-sync-now-btn')) return; // already shown

    const btn = document.createElement('button');
    btn.id          = 'offline-sync-now-btn';
    btn.textContent = '🔄 Sync Now';
    btn.className   = 'btn btn-sm btn-warning';
    btn.title       = 'Tap to sync pending offline transactions';
    btn.style.cssText =
        'position:fixed;bottom:16px;right:16px;z-index:9998;' +
        'box-shadow:0 2px 8px rgba(0,0,0,0.3);';
    btn.addEventListener('click', async () => {
        btn.disabled    = true;
        btn.textContent = '⏳ Syncing…';
        await triggerSync();
        btn.remove();
    });
    document.body.appendChild(btn);
}

function hideSyncNowButton() {
    document.getElementById('offline-sync-now-btn')?.remove();
}

// ── Online/offline indicator update ─────────────────────────────────────────

function updateOnlineIndicator(isOnline) {
    const dot = document.getElementById('online-status-dot');
    if (!dot) return;
    dot.title       = isOnline ? 'Online' : 'Offline';
    dot.className   = isOnline ? 'online-dot online' : 'online-dot offline';
}

// ── Initialise on DOM ready ──────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    updateOnlineIndicator(navigator.onLine);

    if (!navigator.onLine) {
        renderSyncNowButton();
    } else {
        // Check if there are leftover pending transactions from a previous session.
        const count = await window.OfflineQueue.getPendingCount();
        if (count > 0) {
            console.info(`[OfflineSync] Found ${count} pending transaction(s) from previous session. Syncing…`);
            await triggerSync();
        }
    }
});

// Expose for service worker message handler
window.OfflineSync = {
    triggerSync
};

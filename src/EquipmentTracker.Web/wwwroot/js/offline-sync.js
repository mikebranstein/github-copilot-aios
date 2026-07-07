/**
 * offline-sync.js — Auto-sync pending offline transactions when connectivity returns.
 *
 * AC3: Listens for navigator.onLine events and triggers sync automatically.
 *      On Chrome/Firefox: registers a Background Sync tag so the SW can retry even
 *      after the page is closed.
 *      On iOS (no Background Sync API): shows a "Sync Now" button for manual trigger.
 *
 * Depends on: offline-db.js (window.OfflineDB), offline-queue.js (window.OfflineQueue)
 */

const SYNC_TAG       = 'equipment-tracker-sync';
const SYNC_ENDPOINT  = '/api/offline/sync';

// ── Background Sync capability detection ────────────────────────────────────

const supportsBackgroundSync =
    'serviceWorker' in navigator &&
    'SyncManager' in window;

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
            await triggerSync();
        }
    } else {
        // iOS or other browsers without Background Sync — attempt foreground sync,
        // and show "Sync Now" button as fallback UI (see renderSyncNowButton).
        await triggerSync();
    }
}

function handleOffline() {
    updateOnlineIndicator(false);
    console.info('[OfflineSync] Connection lost. Transactions will be queued.');
    renderSyncNowButton(); // Always show the button so users know sync is needed
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

    // Sort chronologically before sending (server also sorts, but belt-and-suspenders).
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
                deviceId            : tx.deviceId
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

    for (const result of results) {
        const dbStatus = result.status === 'success' ? 'synced'
                       : result.status === 'conflict' ? 'conflict'
                       : 'error';
        await window.OfflineDB.updateStatus(
            result.deviceTransactionId,
            dbStatus,
            result.conflictDetails ?? null
        );
        if (result.status === 'conflict') conflicts++;
    }

    await window.OfflineQueue.updateBadge();
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

/**
 * offline-queue.js — Manages the offline transaction queue and enforces queue limits.
 *
 * AC1: Max 50 transactions. Warning displayed at 45. Blocked (error shown) at 50.
 * AC6: Persistence provided by IndexedDB (browser-level guarantee — survives app close).
 *
 * Depends on: offline-db.js (window.OfflineDB)
 */

const QUEUE_WARN_THRESHOLD  = 45;
const QUEUE_MAX             = 50;

/**
 * Generates a RFC-4122 v4 UUID for DeviceTransactionId.
 * Falls back to a timestamp-based ID if crypto.randomUUID is unavailable.
 */
function generateTransactionId() {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
        return crypto.randomUUID();
    }
    // Fallback: timestamp + random suffix
    return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Queues an offline transaction (checkout or return) for later sync.
 *
 * @param {'checkout'|'return'} type
 * @param {number} itemId
 * @param {number} borrowerUserId
 * @param {string} [deviceId] - optional device identifier
 * @returns {Promise<{ok: boolean, error?: string, transactionId?: string}>}
 */
async function enqueueTransaction(type, itemId, borrowerUserId, deviceId = '') {
    const count = await window.OfflineDB.count();

    if (count >= QUEUE_MAX) {
        const msg = `Offline queue is full (${QUEUE_MAX} transactions). ` +
                    `Please reconnect and sync before adding more.`;
        showQueueAlert('danger', msg);
        return { ok: false, error: msg };
    }

    if (count >= QUEUE_WARN_THRESHOLD) {
        showQueueAlert('warning',
            `Offline queue is almost full (${count + 1}/${QUEUE_MAX}). ` +
            `Sync soon to avoid being blocked.`);
    }

    const tx = {
        deviceTransactionId : generateTransactionId(),
        type                : type,
        itemId              : itemId,
        borrowerUserId      : borrowerUserId,
        offlineTimestamp    : new Date().toISOString(),
        deviceId            : deviceId || navigator.userAgent.slice(0, 64),
        status              : 'pending'
    };

    await window.OfflineDB.addTransaction(tx);
    updateQueueBadge();

    return { ok: true, transactionId: tx.deviceTransactionId };
}

/**
 * Shows a dismissible Bootstrap alert in the #offline-queue-alerts container.
 * Creates the container if it doesn't exist.
 */
function showQueueAlert(level, message) {
    let container = document.getElementById('offline-queue-alerts');
    if (!container) {
        container = document.createElement('div');
        container.id = 'offline-queue-alerts';
        container.style.cssText = 'position:fixed;bottom:16px;left:50%;transform:translateX(-50%);' +
                                  'z-index:9999;min-width:320px;max-width:90vw;';
        document.body.appendChild(container);
    }

    const alert = document.createElement('div');
    alert.className = `alert alert-${level} alert-dismissible fade show mb-1`;
    alert.role = 'alert';
    alert.innerHTML =
        `<strong>${level === 'danger' ? '⛔' : '⚠️'}</strong> ${escapeHtml(message)}` +
        `<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>`;
    container.appendChild(alert);

    // Auto-dismiss after 8 seconds
    setTimeout(() => {
        alert.classList.remove('show');
        setTimeout(() => alert.remove(), 300);
    }, 8000);
}

/**
 * Updates the pending-count badge on the navbar offline indicator.
 */
async function updateQueueBadge() {
    const pending = await window.OfflineDB.getPending();
    const badge   = document.getElementById('offline-queue-badge');
    if (badge) {
        badge.textContent = pending.length > 0 ? String(pending.length) : '';
        badge.style.display = pending.length > 0 ? 'inline' : 'none';
    }
}

/**
 * Returns the current pending count without side effects.
 * @returns {Promise<number>}
 */
async function getPendingCount() {
    const pending = await window.OfflineDB.getPending();
    return pending.length;
}

function escapeHtml(str) {
    const d = document.createElement('div');
    d.textContent = str;
    return d.innerHTML;
}

// Initialise badge on DOM ready
document.addEventListener('DOMContentLoaded', updateQueueBadge);

window.OfflineQueue = {
    enqueue          : enqueueTransaction,
    getPendingCount  : getPendingCount,
    updateBadge      : updateQueueBadge,
    showAlert        : showQueueAlert
};

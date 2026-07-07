/**
 * offline-db.js — IndexedDB wrapper for Equipment Tracker offline queue.
 *
 * AC1: Provides a pendingTransactions store backed by IndexedDB.
 * AC6: IndexedDB persistence is a browser-level guarantee — data survives app close
 *      and browser restart without any extra action from application code.
 *
 * Database: EquipmentTrackerOffline  v1
 * Store   : pendingTransactions       keyPath: deviceTransactionId
 */

const DB_NAME    = 'EquipmentTrackerOffline';
const DB_VERSION = 1;
const STORE_NAME = 'pendingTransactions';

/**
 * Opens (or creates) the IndexedDB database and returns a Promise<IDBDatabase>.
 */
function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);

        req.onupgradeneeded = event => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                const store = db.createObjectStore(STORE_NAME, { keyPath: 'deviceTransactionId' });
                store.createIndex('status',            'status',            { unique: false });
                store.createIndex('offlineTimestamp',  'offlineTimestamp',  { unique: false });
            }
        };

        req.onsuccess  = e => resolve(e.target.result);
        req.onerror    = e => reject(e.target.error);
    });
}

/**
 * Adds a pending transaction to the store.
 * @param {object} tx - OfflineSyncTransaction-shaped object with at minimum:
 *   { deviceTransactionId, type, itemId, borrowerUserId, offlineTimestamp, deviceId }
 * @returns {Promise<void>}
 */
async function dbAddTransaction(tx) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t   = db.transaction(STORE_NAME, 'readwrite');
        const s   = t.objectStore(STORE_NAME);
        const req = s.put({ ...tx, status: tx.status ?? 'pending' });
        req.onsuccess = () => resolve();
        req.onerror   = e => reject(e.target.error);
    });
}

/**
 * Returns all transactions in the store (all statuses).
 * @returns {Promise<object[]>}
 */
async function dbGetAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t   = db.transaction(STORE_NAME, 'readonly');
        const s   = t.objectStore(STORE_NAME);
        const req = s.getAll();
        req.onsuccess = e => resolve(e.target.result);
        req.onerror   = e => reject(e.target.error);
    });
}

/**
 * Returns only transactions with status === 'pending'.
 * @returns {Promise<object[]>}
 */
async function dbGetPending() {
    const all = await dbGetAll();
    return all.filter(tx => tx.status === 'pending');
}

/**
 * Updates the status (and optional conflictDetails) of a transaction by ID.
 * @param {string} deviceTransactionId
 * @param {string} status - 'pending' | 'synced' | 'conflict' | 'error'
 * @param {string|null} [conflictDetails]
 * @returns {Promise<void>}
 */
async function dbUpdateStatus(deviceTransactionId, status, conflictDetails = null) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t    = db.transaction(STORE_NAME, 'readwrite');
        const s    = t.objectStore(STORE_NAME);
        const get  = s.get(deviceTransactionId);
        get.onsuccess = e => {
            const record = e.target.result;
            if (!record) { resolve(); return; }
            record.status          = status;
            record.conflictDetails = conflictDetails;
            const put = s.put(record);
            put.onsuccess = () => resolve();
            put.onerror   = ev => reject(ev.target.error);
        };
        get.onerror = e => reject(e.target.error);
    });
}

/**
 * Removes all records from the store (e.g. after a full sync).
 * @returns {Promise<void>}
 */
async function dbClear() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t   = db.transaction(STORE_NAME, 'readwrite');
        const s   = t.objectStore(STORE_NAME);
        const req = s.clear();
        req.onsuccess = () => resolve();
        req.onerror   = e => reject(e.target.error);
    });
}

/**
 * Returns the total count of records in the store.
 * @returns {Promise<number>}
 */
async function dbCount() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t   = db.transaction(STORE_NAME, 'readonly');
        const s   = t.objectStore(STORE_NAME);
        const req = s.count();
        req.onsuccess = e => resolve(e.target.result);
        req.onerror   = e => reject(e.target.error);
    });
}

// Export for use by offline-queue.js and offline-sync.js
window.OfflineDB = {
    addTransaction : dbAddTransaction,
    getAll         : dbGetAll,
    getPending     : dbGetPending,
    updateStatus   : dbUpdateStatus,
    clear          : dbClear,
    count          : dbCount
};

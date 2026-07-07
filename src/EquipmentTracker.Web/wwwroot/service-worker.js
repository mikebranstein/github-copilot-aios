// ── AC2: Cache versioning ────────────────────────────────────────────────────
// Bump CACHE_VERSION when static assets change so old caches are evicted.
const CACHE_VERSION  = 'v2';
const CACHE_NAME     = `equipment-tracker-mobile-${CACHE_VERSION}`;

// AC2: Separate cache for the catalog snapshot so it can be invalidated independently.
const CATALOG_CACHE_NAME  = 'equipment-tracker-catalog-v1';
const CATALOG_SNAPSHOT_URL = '/api/offline/catalog-snapshot';

// Background Sync tag — must match the tag registered in offline-sync.js
const SYNC_TAG = 'equipment-tracker-sync';

const URLS_TO_CACHE = [
    '/',
    '/MobileEquipment',
    '/css/site.css',
    '/js/site.js',
    '/js/offline-db.js',
    '/js/offline-queue.js',
    '/js/offline-sync.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js'
];

// ── Install ──────────────────────────────────────────────────────────────────

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(URLS_TO_CACHE))
            .then(() => self.skipWaiting())
    );
});

// ── Activate ─────────────────────────────────────────────────────────────────

self.addEventListener('activate', event => {
    const keepCaches = [CACHE_NAME, CATALOG_CACHE_NAME];
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(
                keys
                    .filter(key => !keepCaches.includes(key))
                    .map(key => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

// ── Fetch — with special handling for catalog snapshot ───────────────────────

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    // AC2: Cache the catalog snapshot with a dedicated strategy.
    if (event.request.url.includes(CATALOG_SNAPSHOT_URL)) {
        event.respondWith(handleCatalogSnapshot(event.request));
        return;
    }

    // For all other GET requests: network-first, fall back to cache.
    event.respondWith(
        fetch(event.request)
            .then(response => {
                const clone = response.clone();
                caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
                return response;
            })
            .catch(() =>
                caches.match(event.request)
                    .then(cached => cached || caches.match('/MobileEquipment'))
            )
    );
});

/**
 * AC2: Catalog snapshot strategy — network-first, fall back to cache.
 * The cached response is served when offline. The page JS checks
 * the generatedAtUtc field and shows a red warning if >24 h old.
 */
async function handleCatalogSnapshot(request) {
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CATALOG_CACHE_NAME);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        const cached = await caches.match(request, { cacheName: CATALOG_CACHE_NAME });
        if (cached) return cached;
        // Return an empty snapshot so offline UI degrades gracefully.
        return new Response(
            JSON.stringify({ generatedAtUtc: null, items: [] }),
            { headers: { 'Content-Type': 'application/json' } }
        );
    }
}

// ── Background Sync ──────────────────────────────────────────────────────────

/**
 * AC3: Background Sync handler.
 * When the browser wakes the SW to retry a registered sync tag, we post a message
 * to all open clients so offline-sync.js can call triggerSync() with full access
 * to IndexedDB and auth cookies.
 *
 * Note: The SW itself cannot access IndexedDB in a way that reliably reads the
 * auth cookie for the POST; delegating to the open page is the correct pattern.
 */
self.addEventListener('sync', event => {
    if (event.tag === SYNC_TAG) {
        event.waitUntil(notifyClientsToSync());
    }
});

async function notifyClientsToSync() {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: false });
    if (clients.length > 0) {
        // Prefer a focused window; fall back to the first available.
        const target = clients.find(c => c.focused) || clients[0];
        target.postMessage({ type: 'TRIGGER_SYNC' });
    }
    // If no clients are open the sync will be retried by the browser automatically.
}

// ── Push notifications ────────────────────────────────────────────────────────

self.addEventListener('push', event => {
    let payload = { title: 'Equipment Tracker', body: 'You have a new notification.' };

    if (event.data) {
        try {
            payload = event.data.json();
        } catch {
            payload.body = event.data.text();
        }
    }

    event.waitUntil(
        self.registration.showNotification(payload.title, {
            body  : payload.body,
            icon  : '/favicon.ico',
            badge : '/favicon.ico'
        })
    );
});

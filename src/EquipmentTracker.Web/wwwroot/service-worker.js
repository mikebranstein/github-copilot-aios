// ── AC2: Cache versioning ────────────────────────────────────────────────────
// Bump CACHE_VERSION when static assets change so old caches are evicted.
const CACHE_VERSION  = 'v3';
const CACHE_NAME     = `equipment-tracker-mobile-${CACHE_VERSION}`;

// AC2: Separate cache for the catalog snapshot so it can be invalidated independently.
const CATALOG_CACHE_NAME  = 'equipment-tracker-catalog-v2';
const CATALOG_SNAPSHOT_URL = '/api/offline/catalog-snapshot';

// Background Sync tag — must match the tag registered in offline-sync.js
const SYNC_TAG = 'equipment-tracker-sync';

// AC8 (Android Doze Mode): keep-alive ping interval (ms) — 25 s is below Chrome's
// 30 s background timer clamp so the SW is exercised before Doze can freeze it.
const KEEP_ALIVE_INTERVAL_MS = 25000;

// Static assets served with offline-first (cache-first) strategy.
// These are stable resources that should load instantly even offline.
const STATIC_ASSETS = [
    '/',
    '/MobileEquipment',
    '/css/site.css',
    '/js/site.js',
    '/js/offline-db.js',
    '/js/offline-queue.js',
    '/js/offline-sync.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/manifest.webmanifest'
];

// ── Install ──────────────────────────────────────────────────────────────────

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(STATIC_ASSETS))
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

// ── Fetch — offline-first for static assets, network-first for API ───────────

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    const url = event.request.url;

    // AC2: Offline-first for catalog snapshot.
    if (url.includes(CATALOG_SNAPSHOT_URL)) {
        event.respondWith(handleCatalogSnapshot(event.request));
        return;
    }

    // AC2: Offline-first (cache-first) for static assets — load instantly from cache,
    // then revalidate in background (stale-while-revalidate pattern).
    if (isStaticAsset(url)) {
        event.respondWith(handleStaticAsset(event.request));
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
 * AC2: Cache-first (offline-first) strategy for static assets.
 * Serve from cache immediately; revalidate in background.
 */
async function handleStaticAsset(request) {
    const cached = await caches.match(request, { cacheName: CACHE_NAME });
    if (cached) {
        // Revalidate in background (stale-while-revalidate).
        fetch(request)
            .then(fresh => {
                if (fresh && fresh.ok) {
                    caches.open(CACHE_NAME).then(c => c.put(request, fresh));
                }
            })
            .catch(() => { /* offline — use cache */ });
        return cached;
    }
    // Not cached yet — fetch from network and cache it.
    try {
        const response = await fetch(request);
        if (response.ok) {
            const cache = await caches.open(CACHE_NAME);
            cache.put(request, response.clone());
        }
        return response;
    } catch {
        // Absolute fallback — return the shell page from cache.
        return caches.match('/MobileEquipment') || new Response('Offline', { status: 503 });
    }
}

/**
 * Returns true if the URL maps to a known static asset.
 */
function isStaticAsset(url) {
    return STATIC_ASSETS.some(path => url.endsWith(path) || url.includes(path + '?'));
}

/**
 * AC2: Catalog snapshot strategy — offline-first.
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
 * AC5 + AC3: Background Sync handler.
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

// ── AC8: Android Doze Mode keep-alive ping ───────────────────────────────────

/**
 * Periodic self-ping to prevent the service worker from being frozen by Android
 * Doze mode before Persistent Storage (AC1) guarantees data survival.
 * Chrome for Android has a ~30 s idle timer; pinging at 25 s keeps the SW active.
 * This ping also doubles as a lightweight connectivity probe (see offline-sync.js).
 */
self.addEventListener('message', event => {
    if (event.data?.type === 'SW_KEEP_ALIVE') {
        // No-op acknowledgement — receiving the message keeps the SW event loop running.
        event.source?.postMessage({ type: 'SW_KEEP_ALIVE_ACK' });
    }
});

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

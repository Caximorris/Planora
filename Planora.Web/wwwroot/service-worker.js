// Retirement worker for clients that were controlled by a historical Planora PWA.
// Planora no longer offers offline mode: caching the authenticated SPA shell can combine files
// from different deployments. This worker activates once, removes only known Planora cache names,
// and unregisters itself. It deliberately does not reload clients, which could discard edits.
self.addEventListener("install", () => self.skipWaiting());

self.addEventListener("activate", event => {
    event.waitUntil((async () => {
        const cacheNames = await caches.keys();
        await Promise.all(cacheNames
            .filter(name => name.startsWith("planora-")
                || name.startsWith("offline-cache-")
                || name.startsWith("blazor-resources-"))
            .map(name => caches.delete(name)));

        await self.clients.claim();
        await self.registration.unregister();
    })());
});

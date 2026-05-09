self.addEventListener('install', event => {
    event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());
});

self.addEventListener('push', event => {
    const data = event.data ? event.data.json() : {};
    const title = data.title || 'I Heart Fiction';
    const options = {
        body: data.body || '',
        icon: '/_content/IHFiction.SharedWeb/favicon/web-app-manifest-192x192.png',
        badge: '/_content/IHFiction.SharedWeb/favicon/web-app-manifest-192x192.png',
        data: {
            targetPath: data.targetPath || '/'
        }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();

    const targetPath = event.notification.data?.targetPath || '/';
    const targetUrl = new URL(targetPath, self.location.origin).href;

    event.waitUntil((async () => {
        const windows = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if ('focus' in client) {
                await client.focus();
                if ('navigate' in client) {
                    await client.navigate(targetUrl);
                }

                return;
            }
        }

        if (clients.openWindow) {
            await clients.openWindow(targetUrl);
        }
    })());
});

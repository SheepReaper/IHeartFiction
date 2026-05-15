/** @param {InstallEvent} event  */
function onInstall(event) {
    if (!('skipWaiting' in self)) {
        console.warn('Service Worker does not support skipWaiting.');
        return;
    }

    event.waitUntil(self.skipWaiting());
}

/** @param {PushEvent} event  */
function onPush(event) {
    if (!('registration' in self)) {
        console.warn('Service Worker does not support push notifications.');
        return;
    }

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
}

// self.addEventListener('install', onInstall);
self.addEventListener('push', onPush);

(function () {
    const applicationServerPublicKey = 'BEUD5p2W5S7MVXTeiRQwHB-K7eYiItpmaBMiJs8bwcdrv8J7CxSyYCh0Gs8eo2y1iDr6Vt1xjpWu_4yEKwoxCpk';
    const serviceWorkerUrl = '/service-worker.js';

    window.blazorPushNotifications = {
        isSupported: () =>
            'serviceWorker' in navigator &&
            'PushManager' in window &&
            'Notification' in window,

        requestSubscription: async () => {
            if (!window.blazorPushNotifications.isSupported()) {
                return null;
            }

            const permission = await requestNotificationPermission();
            if (permission !== 'granted') {
                return null;
            }

            const worker = await getOrRegisterActiveServiceWorker();
            const existingSubscription = await worker.pushManager.getSubscription();

            if (existingSubscription) {
                return toPushSubscriptionDto(existingSubscription);
            }

            const newSubscription = await subscribe(worker);
            return newSubscription ? toPushSubscriptionDto(newSubscription) : null;
        }
    };

    /** @param {ServiceWorkerRegistration} worker  */
    async function subscribe(worker) {
        try {
            return await worker.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(applicationServerPublicKey)
            });
        } catch (error) {
            if (error.name === 'NotAllowedError') {
                return null;
            }

            throw error;
        }
    }

    async function getOrRegisterActiveServiceWorker() {
        const registration = await navigator.serviceWorker.getRegistration('/')
            ?? await navigator.serviceWorker.register(serviceWorkerUrl, { scope: '/' });

        if (registration.active) {
            return registration;
        }

        const readyRegistration = await navigator.serviceWorker.ready;
        if (readyRegistration.active) {
            return readyRegistration;
        }

        await waitForActiveServiceWorker(registration);
        return registration;
    }

    /** @param {ServiceWorkerRegistration} registration */
    async function waitForActiveServiceWorker(registration) {
        const worker = registration.installing || registration.waiting;
        if (!worker) {
            return;
        }

        if (worker.state === 'activated') {
            return;
        }

        await new Promise((resolve, reject) => {
            const timeout = window.setTimeout(() => {
                worker.removeEventListener('statechange', handleStateChange);
                reject(new Error('Timed out waiting for service worker activation.'));
            }, 10000);

            function handleStateChange() {
                if (worker.state === 'activated') {
                    window.clearTimeout(timeout);
                    worker.removeEventListener('statechange', handleStateChange);
                    resolve();
                }
            }

            worker.addEventListener('statechange', handleStateChange);
        });
    }

    async function requestNotificationPermission() {
        if (Notification.permission !== 'default') {
            return Notification.permission;
        }

        return await Notification.requestPermission();
    }

    /** @param {PushSubscription} subscription */
    function toPushSubscriptionDto(subscription) {
        return {
            endpoint: subscription.endpoint,
            p256dhKey: arrayBufferToBase64(subscription.getKey('p256dh')),
            authKey: arrayBufferToBase64(subscription.getKey('auth')),
            expiresAt: subscription.expirationTime
                ? new Date(subscription.expirationTime).toISOString()
                : null,
            userAgent: navigator.userAgent
        };
    }

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/-/g, '+')
            .replace(/_/g, '/');
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; i++) {
            outputArray[i] = rawData.charCodeAt(i);
        }

        return outputArray;
    }

    /** @param {ArrayBuffer} buffer  */
    function arrayBufferToBase64(buffer) {
        // https://stackoverflow.com/a/9458996
        var binary = '';
        var bytes = new Uint8Array(buffer);
        var len = bytes.byteLength;

        for (var i = 0; i < len; i++) {
            binary += String.fromCharCode(bytes[i]);
        }

        return window.btoa(binary);
    }
})();

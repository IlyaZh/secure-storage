const CACHE_NAME = 'secure-storage-cache-v1.0.3';
const ASSETS = [
  './',
  './index.html',
  './styles.css',
  './config.js',
  './manifest.json',
  './icon.png',
  './src/app.js',
  './src/api.js',
  './src/router.js',
  './src/locales/en.js',
  './src/locales/ru.js',
  './src/locales/i18n.js',
  './src/utils/toast.js',
  './src/utils/crypto.js',
  './src/utils/format.js',
  './src/views/home.js',
  './src/views/login.js',
  './src/views/register.js',
  './src/views/auth-error.js',
  './src/views/create.js',
  './src/views/dashboard.js',
  './src/views/secret.js',
  './screenshot-desktop.png',
  './screenshot-mobile.png'
];

// Install Event
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(ASSETS);
    })
  );
});

// Activate Event (Clean old caches)
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames
          .filter((name) => name !== CACHE_NAME)
          .map((name) => caches.delete(name))
      );
    })
  );
  self.clients.claim();
});

// Fetch Event (Cache-first for static assets)
self.addEventListener('fetch', (event) => {
  // Skip API, non-GET, and non-HTTP/HTTPS requests
  if (
    event.request.method !== 'GET' ||
    event.request.url.includes('/api/') ||
    (!event.request.url.startsWith('http://') && !event.request.url.startsWith('https://'))
  ) {
    return;
  }

  event.respondWith(
    caches.match(event.request).then((cachedResponse) => {
      if (cachedResponse) {
        return cachedResponse;
      }
      return fetch(event.request).then((response) => {
        // Cache dynamically if it is a successful basic static file request
        if (response && response.status === 200 && response.type === 'basic') {
          const responseToCache = response.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(event.request, responseToCache);
          });
        }
        return response;
      });
    })
  );
});

// Message Listener for PWA Auto-Updates
self.addEventListener('message', (event) => {
  if (event.data && event.data.action === 'skipWaiting') {
    self.skipWaiting();
  }
});

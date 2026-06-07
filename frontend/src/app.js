import { registerRoute, handleRoute } from './router.js';
import { checkAuth } from './api.js';
import { renderHome } from './views/home.js';
import { renderLogin } from './views/login.js';
import { renderRegister } from './views/register.js';
import { renderAuthError } from './views/auth-error.js';
import { renderCreateSecret } from './views/create.js';
import { renderDashboard } from './views/dashboard.js';
import { renderViewSecret } from './views/secret.js';
import { t } from './locales/i18n.js';

// Auto-clean service workers and caches on localhost to prevent stale caching
if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.getRegistrations().then(regs => {
      regs.forEach(r => r.unregister());
    });
  }
  if ('caches' in window) {
    window.caches.keys().then(keys => {
      keys.forEach(k => caches.delete(k));
    });
  }
}

// Register SPA Views
registerRoute('#/', renderHome);
registerRoute('#/login', renderLogin);
registerRoute('#/register', renderRegister);
registerRoute('#/auth-error', renderAuthError);
registerRoute('#/create', renderCreateSecret);
registerRoute('#/dashboard', renderDashboard);
registerRoute('#/secret', renderViewSecret);

// Set routing change listener
window.addEventListener('hashchange', handleRoute);

// Bootstrap
document.addEventListener('DOMContentLoaded', async () => {
  // Update logo text translation
  const logoTextEl = document.getElementById('logo-text');
  if (logoTextEl) {
    logoTextEl.innerText = t('nav.logoText');
  }

  // Check session first
  await checkAuth();
  handleRoute();
  
  // Register Service Worker & update handler
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js')
      .then(reg => {
        reg.addEventListener('updatefound', () => {
          const newWorker = reg.installing;
          newWorker.addEventListener('statechange', () => {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              // New update is ready and waiting to skipWaiting
              showUpdateBanner(reg);
            }
          });
        });
        
        // If there's already a waiting worker on page load
        if (reg.waiting) {
          showUpdateBanner(reg);
        }
      })
      .catch(err => console.error("ServiceWorker registration failed", err));
  }
});

function showUpdateBanner(registration) {
  // Check if banner already exists
  if (document.getElementById('pwa-update-banner')) return;

  const banner = document.createElement('div');
  banner.id = 'pwa-update-banner';
  banner.className = 'update-banner';
  banner.innerHTML = `
    <span>${t('pwa.updateBanner')}</span>
    <button class="update-btn" id="pwa-update-btn">${t('pwa.updateButton')}</button>
  `;
  document.body.appendChild(banner);

  document.getElementById('pwa-update-btn').onclick = () => {
    // Send message to skip waiting
    if (registration.waiting) {
      registration.waiting.postMessage({ action: 'skipWaiting' });
    }
    banner.remove();
  };

  // Reload page once new service worker becomes active
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    window.location.reload();
  });
}

import { t } from '../locales/i18n.js';
import { state } from '../api.js';
import { navigate } from '../router.js';

export function renderHome() {
  if (state.currentUser.isAuthenticated) {
    navigate('/dashboard');
    return;
  }

  const appContainer = document.getElementById('app');
  appContainer.innerHTML = `
    <div class="hero-section">
      <h1 class="hero-title">${t('home.title')}</h1>
      <p class="hero-desc">${t('home.desc')}</p>
      <div style="display:flex; justify-content:center; gap: 1.5rem;">
        <a href="#/create" class="btn btn-primary">${t('home.createBtn')}</a>
        <a href="#/dashboard" class="btn btn-secondary">${t('home.mySecretsBtn')}</a>
      </div>
    </div>
  `;
}


import { t } from '../locales/i18n.js';

export function renderAuthError(params = {}) {
  const appContainer = document.getElementById('app');
  appContainer.innerHTML = `
    <div class="card" style="max-width: 550px; margin: 3rem auto; text-align: center;">
      <span style="font-size: 3.5rem; display: block; margin-bottom: 1rem; animation: pulse-balloon 2s infinite;">⚠️</span>
      <h2 class="card-title">${t('authError.title')}</h2>
      <p class="card-desc" style="margin-bottom: 2rem;">${t('authError.desc')}</p>
      
      <a href="#/login" class="btn btn-primary" style="display: inline-flex; width: auto; min-width: 200px; justify-content: center;">
        ${t('authError.backBtn')}
      </a>
    </div>
  `;
}

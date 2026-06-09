import { t } from '../locales/i18n.js';
import { showToast } from '../utils/toast.js';

export function renderLogin(params = {}) {
  const appContainer = document.getElementById('app');
  appContainer.innerHTML = `
    <div class="card" style="max-width: 500px; margin: 2rem auto;">
      <h2 class="card-title">${t('login.title')}</h2>
      <p class="card-desc" style="margin-bottom:1.5rem;">${t('login.desc')}</p>
      
      <button id="google-login-btn" class="btn btn-primary btn-block">${t('login.googleBtn')}</button>
    </div>
  `;

  document.getElementById('google-login-btn').onclick = () => {
    window.location.href = `${window.ENV.API_URL}/api/auth/login`;
  };

  if (params && params.error) {
    let errMsg = t('toast.networkError');
    if (params.error === 'auth_failed') errMsg = t('toast.authFailedError');
    else if (params.error === 'email_missing') errMsg = t('toast.emailMissingError');
    
    setTimeout(() => showToast(errMsg, "error"), 50);
  }
}

import { t } from '../locales/i18n.js';
import { showToast } from '../utils/toast.js';

export function renderRegister(params = {}) {
  // Check if we have an invite code passed either as "invite" or "inviteCode"
  const inviteCode = params.invite || params.inviteCode || '';

  const appContainer = document.getElementById('app');
  appContainer.innerHTML = `
    <div class="card" style="max-width: 500px; margin: 2rem auto;">
      <h2 class="card-title">${t('register.title')}</h2>
      <p class="card-desc" style="margin-bottom:1.5rem;">${t('register.desc')}</p>
      
      <div class="form-group">
        <label class="form-label" for="invite-code">${t('register.inviteLabel')}</label>
        <input class="form-input" type="text" id="invite-code" value="${inviteCode}" placeholder="${t('register.invitePlaceholder')}">
      </div>

      <button id="register-btn" class="btn btn-primary btn-block">${t('register.submitBtn')}</button>
    </div>
  `;

  document.getElementById('register-btn').onclick = () => {
    const code = document.getElementById('invite-code').value.trim();
    if (!code) {
      showToast(t('toast.inviteRequiredError') || "Invite code is required", "warning");
      return;
    }
    
    // Redirect to backend auth login with invite code
    window.location.href = `${window.ENV.API_URL}/api/auth/login?inviteCode=${encodeURIComponent(code)}`;
  };

  // If there's an error param in url
  if (params && params.error) {
    let errMsg = t('toast.networkError');
    if (params.error === 'invite_invalid') {
      errMsg = t('toast.inviteInvalidError');
    } else if (params.error === 'email_mismatch') {
      errMsg = t('toast.emailMismatchError');
    } else if (params.error === 'already_registered') {
      errMsg = t('toast.inviteCreateUserExistsError');
    }
    setTimeout(() => showToast(errMsg, "error"), 50);
  }
}

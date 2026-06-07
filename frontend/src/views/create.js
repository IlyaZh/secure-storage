import { t } from '../locales/i18n.js';
import { showToast } from '../utils/toast.js';
import { encryptData, arrayBufferToBase64, bufToHex } from '../utils/crypto.js';
import { apiFetch, state } from '../api.js';
import { navigate } from '../router.js';

export function renderCreateSecret() {
  if (!state.currentUser.isAuthenticated) {
    showToast(t('toast.authWarning'), "warning");
    navigate('/login');
    return;
  }

  const appContainer = document.getElementById('app');
  appContainer.innerHTML = `
    <div class="card">
      <h2 class="card-title">${t('create.title')}</h2>
      <p class="card-desc">${t('create.desc')}</p>
      
      <div class="form-group">
        <label class="checkbox-label">
          <input type="checkbox" id="is-file-checkbox" class="checkbox-input">
          ${t('create.checkboxFile')}
        </label>
      </div>

      <!-- Text input -->
      <div class="form-group" id="text-input-group">
        <label class="form-label" for="secret-text">${t('create.textLabel')}</label>
        <textarea class="form-input" id="secret-text" placeholder="${t('create.textPlaceholder')}"></textarea>
      </div>

      <!-- File input -->
      <div class="form-group" id="file-input-group" style="display:none;">
        <label class="form-label">${t('create.fileLabel')}</label>
        <div class="file-upload-container" id="file-upload-trigger">
          <span class="file-upload-icon">📁</span>
          <span class="file-upload-text" id="file-name-text">${t('create.fileSelect')}</span>
          <input type="file" id="secret-file" class="file-upload-input">
        </div>
      </div>

      <div class="form-group">
        <label class="form-label" for="secret-comment">${t('create.commentLabel')}</label>
        <input class="form-input" type="text" id="secret-comment" placeholder="${t('create.commentPlaceholder')}">
      </div>

      <div class="form-group" style="display:flex; gap: 2rem;">
        <label class="checkbox-label">
          <input type="checkbox" id="secret-onetime" class="checkbox-input" checked>
          ${t('create.onetimeLabel')}
        </label>
      </div>

      <button id="submit-secret-btn" class="btn btn-primary">${t('create.submitBtn')}</button>

      <div id="result-link-area" style="display:none; margin-top:2rem;">
        <h3 style="margin-bottom:0.5rem; font-size:1.1rem;">${t('create.resultTitle')}</h3>
        <p class="card-desc" style="color:var(--accent-color); font-size:0.875rem; margin-bottom:0.5rem;">${t('create.resultWarning')}</p>
        <div class="secret-link-box">
          <span class="secret-link-text" id="result-link-text"></span>
          <button id="copy-result-link-btn" class="btn btn-secondary" style="padding:0.5rem 1rem;">${t('create.copyBtn')}</button>
        </div>
      </div>
    </div>
  `;

  // Attach handlers
  const fileCheckbox = document.getElementById('is-file-checkbox');
  fileCheckbox.onchange = () => {
    const isFile = fileCheckbox.checked;
    document.getElementById('text-input-group').style.display = isFile ? 'none' : 'block';
    document.getElementById('file-input-group').style.display = isFile ? 'block' : 'none';
  };

  const fileUploadTrigger = document.getElementById('file-upload-trigger');
  const fileInput = document.getElementById('secret-file');
  fileUploadTrigger.onclick = () => fileInput.click();
  fileInput.onchange = (e) => {
    const file = e.target.files[0];
    if (file) {
      document.getElementById('file-name-text').innerText = `${file.name} (${(file.size / 1024).toFixed(1)} KB)`;
    }
  };

  document.getElementById('submit-secret-btn').onclick = submitSecret;
}

async function submitSecret() {
  const submitBtn = document.getElementById('submit-secret-btn');
  const isFile = document.getElementById('is-file-checkbox').checked;
  const comment = document.getElementById('secret-comment').value.trim();
  const isOneTime = document.getElementById('secret-onetime').checked;
  
  if (!comment) {
    showToast(t('toast.emptyComment'), "warning");
    return;
  }

  let plainDataBytes;
  let fileName = null;
  let contentType = 'text/plain';

  if (isFile) {
    const fileInput = document.getElementById('secret-file');
    const file = fileInput.files[0];
    if (!file) {
      showToast(t('toast.chooseFile'), "warning");
      return;
    }
    fileName = file.name;
    contentType = file.type || 'application/octet-stream';
    const arrayBuffer = await file.arrayBuffer();
    plainDataBytes = new Uint8Array(arrayBuffer);
  } else {
    const text = document.getElementById('secret-text').value.trim();
    if (!text) {
      showToast(t('toast.emptyText'), "warning");
      return;
    }
    plainDataBytes = new TextEncoder().encode(text);
  }

  submitBtn.disabled = true;
  submitBtn.innerText = t('create.loadingEncrypt');

  try {
    const rawKeyBytes = crypto.getRandomValues(new Uint8Array(32));
    const { ciphertext, iv } = await encryptData(plainDataBytes, rawKeyBytes);

    const headers = {
      'X-Secret-Comment': comment,
      'X-Secret-IsOneTime': isOneTime.toString(),
      'X-Secret-ContentType': contentType,
      'X-Secret-IV': arrayBufferToBase64(iv)
    };
    if (fileName) {
      headers['X-Secret-FileName'] = fileName;
    }

    const response = await apiFetch('/api/secrets', {
      method: 'POST',
      headers: headers,
      body: ciphertext
    });

    const result = await response.json();
    const hexKey = bufToHex(rawKeyBytes);
    const secretLink = `${window.location.origin}/#/secret/${result.id}:${hexKey}`;

    document.getElementById('result-link-text').innerText = secretLink;
    document.getElementById('result-link-area').style.display = 'block';
    
    showToast(t('toast.createSuccess'), "success");
    
    document.getElementById('copy-result-link-btn').onclick = () => {
      navigator.clipboard.writeText(secretLink);
      showToast(t('toast.copySuccess'), "success");
    };

    submitBtn.innerText = t('home.createBtn');
    submitBtn.disabled = false;
    submitBtn.onclick = () => renderCreateSecret();
  } catch (error) {
    submitBtn.disabled = false;
    submitBtn.innerText = t('create.submitBtn');
    if (error.message && error.message.includes("size exceeds")) {
      showToast(t('toast.sizeLimitError'), "error");
    } else {
      showToast(t('toast.createError'), "error");
    }
  }
}

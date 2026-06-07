import { showToast } from './utils/toast.js';
import { navigate } from './router.js';

import { t } from './locales/i18n.js';

export const state = {
  currentUser: {
    isAuthenticated: false,
    email: null,
    id: null
  }
};

export async function apiFetch(path, options = {}) {
  const url = `${window.ENV.API_URL}${path}`;
  options.credentials = 'include';
  
  try {
    const response = await fetch(url, options);
    if (response.status === 401) {
      state.currentUser.isAuthenticated = false;
      navigate('/login');
      throw new Error("Unauthorized");
    }
    return response;
  } catch (error) {
    if (error.message !== "Unauthorized") {
      showToast(t('toast.networkError') || "Network error or connection failed.", "error");
    }
    throw error;
  }
}

export async function checkAuth() {
  try {
    const response = await apiFetch('/api/auth/me');
    const data = await response.json();
    state.currentUser.isAuthenticated = data.isAuthenticated;
    state.currentUser.email = data.email || null;
    state.currentUser.id = data.id || null;
    updateNavUI();
  } catch (e) {
    state.currentUser.isAuthenticated = false;
  }
}

export function updateNavUI() {
  const navContainer = document.getElementById('nav-links');
  if (!navContainer) return;

  if (state.currentUser.isAuthenticated) {
    navContainer.innerHTML = `
      <span style="font-size:0.9rem; color: var(--text-muted);">${state.currentUser.email}</span>
      <a href="#/dashboard" class="nav-btn secondary">${t('nav.cabinet')}</a>
      <a href="#/create" class="nav-btn primary">${t('nav.create')}</a>
      <button id="logout-btn" class="nav-btn secondary" style="background:none; border:none; cursor:pointer;">${t('nav.logout')}</button>
    `;
    const logoutBtn = document.getElementById('logout-btn');
    if (logoutBtn) {
      logoutBtn.onclick = logout;
    }
  } else {
    navContainer.innerHTML = `
      <a href="#/login" class="nav-btn primary">${t('nav.login')}</a>
    `;
  }
}

export async function logout() {
  try {
    await apiFetch('/api/auth/logout');
    state.currentUser.isAuthenticated = false;
    state.currentUser.email = null;
    state.currentUser.id = null;
    showToast(t('toast.logoutSuccess'), "info");
    updateNavUI();
    navigate('/');
  } catch (e) {}
}

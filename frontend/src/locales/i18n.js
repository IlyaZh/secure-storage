import { en } from './en.js';
import { ru } from './ru.js';

const translations = { en, ru };

// Detect language
let lang = localStorage.getItem('lang');
if (!lang) {
  const browserLang = navigator.language || navigator.userLanguage;
  if (browserLang) {
    const shortLang = browserLang.split('-')[0].toLowerCase();
    if (translations[shortLang]) {
      lang = shortLang;
    }
  }
  if (!lang) {
    lang = 'en';
  }
}

const currentDict = translations[lang] || en;

export function t(key) {
  // Resolve dot notation like 'home.title'
  const val = key.split('.').reduce((obj, k) => (obj && obj[k] !== undefined) ? obj[k] : undefined, currentDict);
  return val !== undefined ? val : key;
}

export function getLanguage() {
  return lang;
}

export function setLanguage(newLang) {
  if (translations[newLang]) {
    localStorage.setItem('lang', newLang);
    window.location.reload();
  }
}

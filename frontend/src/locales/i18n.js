import { en } from './en.js';
import { ru } from './ru.js';

const translations = { en, ru };

// Detect language
let lang = 'en';
const browserLang = navigator.language || navigator.userLanguage;
if (browserLang) {
  const shortLang = browserLang.split('-')[0].toLowerCase();
  if (translations[shortLang]) {
    lang = shortLang;
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

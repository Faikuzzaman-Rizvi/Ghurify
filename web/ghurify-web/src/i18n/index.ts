import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import bn from './bn.json';
import en from './en.json';

export const supportedLanguages = ['bn', 'en'] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

/**
 * Bangla is the default: most travellers read Bangla first, and English is the fallback
 * only when the browser asks for it.
 */
export const defaultLanguage: SupportedLanguage = 'bn';

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      bn: { translation: bn },
      en: { translation: en },
    },
    fallbackLng: defaultLanguage,
    supportedLngs: [...supportedLanguages],
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'ghurify.language',
      caches: ['localStorage'],
    },
    interpolation: {
      // React already escapes rendered values.
      escapeValue: false,
    },
  });

export default i18n;

import i18n, { type BackendModule, type ReadCallback, type Services } from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import bn from './bn.json';

export const supportedLanguages = ['bn', 'en'] as const;
export type SupportedLanguage = (typeof supportedLanguages)[number];

/**
 * Bangla is the default: most travellers read Bangla first, and English is the fallback
 * only when the browser asks for it.
 */
export const defaultLanguage: SupportedLanguage = 'bn';

/**
 * Bangla ships inside the app's own code and English is fetched the first time somebody asks for
 * it. The two files together are about 270 kB of JSON, and bundling both (as this did) made every
 * visitor download a language they were not reading.
 *
 * It is this way round rather than both being fetched because Bangla is the default: having it
 * already in memory means the first paint has real text in it without waiting for a second
 * request. An English reader pays one fetch, once, and it is cached from then on.
 */
const fetched: Record<string, () => Promise<{ default: Record<string, unknown> }>> = {
  en: () => import('./en.json'),
};

/**
 * Hands i18next the English file when it first asks for it. i18next waits for this before it
 * reports the language as ready, so switching never shows raw keys: `changeLanguage` resolves
 * once the file has arrived.
 *
 * Written inline rather than pulled from a package: it is a dozen lines, and the alternative
 * (i18next-resources-to-backend) would be a dependency for exactly this.
 */
const lazyBundles: BackendModule = {
  type: 'backend',
  init: (_services?: Services, _backendOptions?: unknown, _i18nextOptions?: unknown) => {
    // Nothing to set up: everything this needs is in `fetched`.
  },
  read: (language: string, _namespace: string, callback: ReadCallback) => {
    const load = fetched[language];

    if (!load) {
      // Bangla is already bundled, and i18next also probes region variants ("en-GB"). Reporting
      // "nothing here, and do not retry" lets it use what it has.
      callback(null, false);
      return;
    }

    load()
      .then((module) => callback(null, module.default))
      .catch((error: unknown) => callback(error as Error, false));
  },
};

// Keeps <html lang> in step with the language, including the one restored on load: screen
// readers pick their voice from it, and the styles drop letter-spacing for Bangla by it.
i18n.on('languageChanged', (language) => {
  if (typeof document !== 'undefined') {
    document.documentElement.lang = language;
  }
});

/**
 * Starts i18next and resolves once the active language's strings are in memory: immediately for
 * Bangla, after one fetch for English.
 *
 * Awaited before the app renders (and before the tests run), so the first paint has real text in
 * it rather than keys that are replaced a moment later.
 */
export const i18nReady: Promise<unknown> = i18n
  .use(lazyBundles)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    // Bangla is here rather than behind the backend, so it needs no request.
    resources: {
      bn: { translation: bn },
    },
    // ...but the backend is still asked for any language that is not in `resources`.
    partialBundledLanguages: true,
    fallbackLng: defaultLanguage,
    supportedLngs: [...supportedLanguages],
    // "bn-BD" is served by the "bn" file; without this i18next would ask for the region variant.
    load: 'languageOnly',
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'ghurify.language',
      caches: ['localStorage'],
    },
    interpolation: {
      // React already escapes rendered values.
      escapeValue: false,
    },
    react: {
      // The strings are loaded before the first render and before a language switch completes,
      // so there is never a pending state for Suspense to show.
      useSuspense: false,
    },
  });

export default i18n;

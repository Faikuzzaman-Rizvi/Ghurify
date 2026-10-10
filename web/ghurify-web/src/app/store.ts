import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import i18n, { defaultLanguage, supportedLanguages, type SupportedLanguage } from '@/i18n';

/**
 * Small global client state only: language now, signed-in user and theme later.
 * Server data belongs in TanStack Query, never here.
 */
interface AppState {
  language: SupportedLanguage;
  setLanguage: (language: SupportedLanguage) => void;
}

/** The language i18next is actually rendering, as one of the two codes this app has strings for. */
function current(): SupportedLanguage {
  // The region has to come off first: a browser set to English reports "en-US", and the strings
  // for it are the "en" ones (i18next is configured with load: 'languageOnly'). Comparing the
  // full tag against the two supported codes matches neither.
  const base = (i18n.resolvedLanguage ?? i18n.language ?? '').split('-')[0] ?? '';

  return (supportedLanguages as readonly string[]).includes(base)
    ? (base as SupportedLanguage)
    : defaultLanguage;
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      // A placeholder only: i18next has not settled its language by the time this module is
      // evaluated (it may still be fetching the strings). The subscription below is what puts
      // the real answer in, as soon as there is one.
      language: current(),
      setLanguage: (language) => {
        void i18n.changeLanguage(language);
        document.documentElement.lang = language;
        set({ language });
      },
    }),
    { name: 'ghurify.app' },
  ),
);

/**
 * i18next owns which language is in use: it persists the reader's choice itself and falls back to
 * the browser's preference, and it settles both asynchronously. Mirroring it here — rather than
 * reading it once at startup — is what keeps the header's switch honest: it used to show Bangla
 * selected on a page rendered in English, because the store had guessed before i18next answered.
 */
i18n.on('languageChanged', () => {
  const language = current();

  if (useAppStore.getState().language !== language) {
    useAppStore.setState({ language });
  }
});

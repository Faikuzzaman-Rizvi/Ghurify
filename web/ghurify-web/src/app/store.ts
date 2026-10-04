import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import i18n, { defaultLanguage, type SupportedLanguage } from '@/i18n';

/**
 * Small global client state only: language now, signed-in user and theme later.
 * Server data belongs in TanStack Query, never here.
 */
interface AppState {
  language: SupportedLanguage;
  setLanguage: (language: SupportedLanguage) => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      language: defaultLanguage,
      setLanguage: (language) => {
        void i18n.changeLanguage(language);
        document.documentElement.lang = language;
        set({ language });
      },
    }),
    { name: 'ghurify.app' },
  ),
);

import { useTranslation } from 'react-i18next';
import { useAppStore } from '@/app/store';
import { supportedLanguages, type SupportedLanguage } from '@/i18n';

const labelKey: Record<SupportedLanguage, string> = {
  bn: 'language.bangla',
  en: 'language.english',
};

/** Switches between Bangla and English. The choice is remembered across visits. */
export function LanguageToggle() {
  const { t } = useTranslation();
  const language = useAppStore((state) => state.language);
  const setLanguage = useAppStore((state) => state.setLanguage);

  return (
    <div className="flex items-center gap-2">
      <span id="language-label" className="text-sm text-deep/70">
        {t('language.label')}
      </span>
      <div role="group" aria-labelledby="language-label" className="flex gap-1">
        {supportedLanguages.map((code) => (
          <button
            key={code}
            type="button"
            onClick={() => setLanguage(code)}
            aria-pressed={language === code}
            className={`rounded-md px-3 py-1 text-sm transition ${
              language === code
                ? 'bg-hill text-white'
                : 'bg-white text-deep hover:bg-hill/10 border border-hill/20'
            }`}
          >
            {t(labelKey[code])}
          </button>
        ))}
      </div>
    </div>
  );
}

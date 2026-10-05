import { useTranslation } from 'react-i18next';
import { useAppStore } from '@/app/store';
import { supportedLanguages, type SupportedLanguage } from '@/i18n';

const labelKey: Record<SupportedLanguage, string> = {
  bn: 'language.bangla',
  en: 'language.english',
};

/** Short labels for the compact header switch: each written in its own script. */
const shortLabel: Record<SupportedLanguage, string> = {
  bn: 'বাং',
  en: 'EN',
};

/** Switches between Bangla and English. The choice is remembered across visits. */
export function LanguageToggle({ compact = false }: { compact?: boolean }) {
  const { t } = useTranslation();
  const language = useAppStore((state) => state.language);
  const setLanguage = useAppStore((state) => state.setLanguage);

  return (
    <div className="flex items-center gap-2">
      <span id="language-label" className={compact ? 'sr-only' : 'text-sm text-deep/70'}>
        {t('language.label')}
      </span>
      <div
        role="group"
        aria-labelledby="language-label"
        className="flex gap-0.5 rounded-full border border-hill/20 bg-white p-0.5"
      >
        {supportedLanguages.map((code) => (
          <button
            key={code}
            type="button"
            onClick={() => setLanguage(code)}
            aria-pressed={language === code}
            aria-label={compact ? t(labelKey[code]) : undefined}
            className={`rounded-full px-3 py-1 text-sm transition ${
              language === code ? 'bg-hill text-white' : 'text-deep hover:bg-hill/10'
            }`}
          >
            {compact ? shortLabel[code] : t(labelKey[code])}
          </button>
        ))}
      </div>
    </div>
  );
}

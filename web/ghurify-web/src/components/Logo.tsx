import { useTranslation } from 'react-i18next';

/** The Ghurify mark: a sun rising over two hills, with the name beside it. */
export function Logo({ inverted = false }: { inverted?: boolean }) {
  const { t } = useTranslation();

  return (
    <span className="flex items-center gap-2">
      <svg viewBox="0 0 40 40" className="h-9 w-9 shrink-0" aria-hidden="true" focusable="false">
        <rect width="40" height="40" rx="12" fill={inverted ? '#ffffff' : '#245c43'} />
        <circle cx="25" cy="15" r="6" fill="#d99a12" />
        <path
          d="M4 32 Q 12 18 20 26 Q 27 17 36 30 V36 H4Z"
          fill={inverted ? '#245c43' : '#ffffff'}
          opacity="0.95"
        />
      </svg>
      <span
        className={`font-display text-2xl font-extrabold tracking-tight ${
          inverted ? 'text-white' : 'text-hill'
        }`}
      >
        {t('app.name')}
      </span>
    </span>
  );
}

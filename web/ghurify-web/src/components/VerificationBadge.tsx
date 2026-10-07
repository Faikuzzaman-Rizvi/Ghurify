import { useTranslation } from 'react-i18next';
import { BadgeCheck } from 'lucide-react';
import type { components } from '@/api/schema';

type VerificationLevel = NonNullable<components['schemas']['VerificationLevel']>;

const style: Record<VerificationLevel, string> = {
  Phone: 'bg-sky text-river ring-river/20',
  Nid: 'bg-hill/10 text-hill ring-hill/25',
  NidSelfie: 'bg-hill text-white ring-hill',
};

/**
 * The badge a verified person earns. Derived from the strongest identity check they have
 * passed; nothing is shown for someone unverified, rather than a badge that says "no".
 */
export function VerificationBadge({
  level,
  size = 'sm',
}: {
  level: VerificationLevel | null | undefined;
  size?: 'sm' | 'md';
}) {
  const { t } = useTranslation();

  if (!level) {
    return null;
  }

  return (
    <span
      title={t(`verification.badgeHint.${level}`)}
      className={`inline-flex items-center gap-1 rounded-full font-semibold ring-1 ${style[level]} ${
        size === 'md' ? 'px-3 py-1 text-sm' : 'px-2 py-0.5 text-xs'
      }`}
    >
      <BadgeCheck aria-hidden="true" className={size === 'md' ? 'h-4 w-4' : 'h-3.5 w-3.5'} />
      {t(`verification.badge.${level}`)}
    </span>
  );
}

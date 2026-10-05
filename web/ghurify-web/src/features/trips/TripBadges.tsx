import { useTranslation } from 'react-i18next';
import { formatCount, toLanguage } from '@/lib/format';
import type { DestinationStatus, GroupType } from './tripsApi';

const groupStyle: Record<GroupType, string> = {
  Open: 'bg-white/90 text-deep',
  WomenOnly: 'bg-jamdani text-white',
  Students: 'bg-river text-white',
  Families: 'bg-turmeric text-deep',
};

/** Who a trip is for. Women-only stands out: it is a safety feature, not a footnote. */
export function GroupBadge({ groupType }: { groupType: GroupType }) {
  const { t } = useTranslation();

  return (
    <span
      className={`rounded-full px-3 py-1 text-xs font-semibold shadow-sm ${groupStyle[groupType]}`}
    >
      {t(`groupType.${groupType}`)}
    </span>
  );
}

/** Seats left, turning urgent when only a few remain. */
export function SeatsBadge({ seatsLeft }: { seatsLeft: number }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  if (seatsLeft <= 0) {
    return (
      <span className="rounded-full bg-deep/10 px-3 py-1 text-xs font-semibold text-deep/70">
        {t('trips.full')}
      </span>
    );
  }

  const urgent = seatsLeft <= 3;

  return (
    <span
      className={`rounded-full px-3 py-1 text-xs font-semibold ${
        urgent ? 'bg-jamdani/10 text-jamdani' : 'bg-hill/10 text-hill'
      }`}
    >
      {/* `count` picks the plural form; `n` is the same number written in the reader's script. */}
      {t('trips.seatsLeft', { count: seatsLeft, n: formatCount(seatsLeft, language) })}
    </span>
  );
}

const statusStyle: Record<DestinationStatus, string> = {
  Open: 'bg-emerald-50 text-emerald-800 ring-emerald-600/20',
  Caution: 'bg-amber-50 text-amber-800 ring-amber-600/30',
  Closed: 'bg-red-50 text-red-800 ring-red-600/30',
};

const statusDot: Record<DestinationStatus, string> = {
  Open: 'bg-emerald-500',
  Caution: 'bg-amber-500',
  Closed: 'bg-red-500',
};

/** A destination's live safety status. */
export function StatusBadge({ status }: { status: DestinationStatus }) {
  const { t } = useTranslation();

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ${statusStyle[status]}`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${statusDot[status]}`} aria-hidden="true" />
      {t(`destination.status.${status}`)}
    </span>
  );
}

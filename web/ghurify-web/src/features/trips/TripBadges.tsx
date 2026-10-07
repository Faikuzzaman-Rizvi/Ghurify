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

/**
 * Seats left, turning urgent when only a few remain. `onPhoto` gives it a solid background
 * so it stays legible over a picture.
 */
export function SeatsBadge({
  seatsLeft,
  onPhoto = false,
}: {
  seatsLeft: number;
  onPhoto?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  if (seatsLeft <= 0) {
    return (
      <span
        className={`rounded-full px-3 py-1 text-xs font-semibold ${
          onPhoto ? 'bg-white/90 text-deep' : 'bg-deep/10 text-deep/70'
        }`}
      >
        {t('trips.full')}
      </span>
    );
  }

  const urgent = seatsLeft <= 3;
  const style = onPhoto
    ? urgent
      ? 'bg-jamdani text-white'
      : 'bg-white/90 text-hill'
    : urgent
      ? 'bg-jamdani/10 text-jamdani'
      : 'bg-hill/10 text-hill';

  return (
    <span className={`whitespace-nowrap rounded-full px-3 py-1 text-xs font-semibold ${style}`}>
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

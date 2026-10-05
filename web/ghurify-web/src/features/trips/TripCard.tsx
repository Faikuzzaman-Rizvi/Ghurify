import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { asNumber } from '@/api/client';
import { Scenery } from '@/components/Scenery';
import { formatCount, formatDateRange, formatMoney, toLanguage, tripDays } from '@/lib/format';
import { GroupBadge, SeatsBadge } from './TripBadges';
import type { TripSummary } from './tripsApi';

/** One trip in a grid: where, when, how many seats are left, and the price per person. */
export function TripCard({ trip }: { trip: TripSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const id = asNumber(trip.id);
  const seatsLeft = asNumber(trip.seatsLeft);
  const place = language === 'bn' ? trip.destination.nameBn : trip.destination.name;
  const days = tripDays(trip.startDate, trip.endDate);

  return (
    <article className="group relative flex flex-col overflow-hidden rounded-3xl bg-white shadow-sm ring-1 ring-hill/10 transition hover:-translate-y-1 hover:shadow-xl">
      <div className="relative h-40 overflow-hidden">
        <Scenery
          kind={trip.destination.kind}
          className="h-full w-full transition duration-500 group-hover:scale-105"
        />
        <div className="absolute inset-x-3 top-3 flex items-start justify-between gap-2">
          <span className="rounded-full bg-white/90 px-3 py-1 text-xs font-semibold text-deep shadow-sm">
            📍 {place}
          </span>
          <GroupBadge groupType={trip.groupType} />
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-3 p-5">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-turmeric">
            {formatDateRange(trip.startDate, trip.endDate, language)} ·{' '}
            {t('trips.days', { count: days, n: formatCount(days, language) })}
          </p>
          <h3 className="mt-1 text-lg font-bold text-deep">
            {/* The whole card is clickable through this link's stretched overlay. */}
            <Link to={`/trips/${id}`} className="after:absolute after:inset-0 focus:outline-none">
              {trip.title}
            </Link>
          </h3>
          <p className="mt-1 text-sm text-deep/60">
            {t('trips.hostedBy', { name: trip.hostName ?? t('trips.aHost') })}
          </p>
        </div>

        <div className="mt-auto flex items-end justify-between gap-2">
          <SeatsBadge seatsLeft={seatsLeft} />
          <p className="text-right">
            <span className="block font-display text-2xl font-extrabold leading-none text-hill">
              {formatMoney(trip.pricePerPerson, language)}
            </span>
            <span className="text-xs text-deep/60">{t('trips.perPerson')}</span>
          </p>
        </div>
      </div>
    </article>
  );
}

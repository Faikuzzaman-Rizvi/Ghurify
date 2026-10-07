import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { CalendarDays, MapPin } from 'lucide-react';
import { asNumber } from '@/api/client';
import { Photo } from '@/components/ui/Photo';
import { VerificationBadge } from '@/components/VerificationBadge';
import { formatCount, formatDateRange, formatMoney, toLanguage, tripDays } from '@/lib/format';
import { GroupBadge, SeatsBadge } from './TripBadges';
import type { TripSummary } from './tripsApi';

/**
 * One trip as a tall photo card: the place's photo, the price on a vertical tag, and the
 * title, dates and seats over a dark fade at the foot. The whole card is one link.
 */
export function TripCard({ trip }: { trip: TripSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const id = asNumber(trip.id);
  const seatsLeft = asNumber(trip.seatsLeft);
  const place = language === 'bn' ? trip.destination.nameBn : trip.destination.name;
  const days = tripDays(trip.startDate, trip.endDate);

  return (
    <article className="group relative isolate flex aspect-4/5 flex-col justify-end overflow-hidden rounded-2xl bg-night text-white shadow-sm max-h-112 transition duration-500 hover:-translate-y-1 hover:shadow-2xl focus-within:ring-4 focus-within:ring-turmeric/60">
      <Photo
        slug={trip.destination.slug}
        kind={trip.destination.kind}
        cut="card"
        decorative
        className="absolute! inset-0 -z-10"
        imgClassName="photo-zoom"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-t from-night via-night/45 to-transparent"
      />

      <div className="absolute left-4 top-4">
        <GroupBadge groupType={trip.groupType} />
      </div>

      {/* The price tag, read bottom to top like a luggage label. */}
      <p className="absolute right-4 top-0 flex rotate-180 items-center gap-2 rounded-t-md bg-turmeric px-2 py-3 font-display text-sm font-bold text-night shadow-md [writing-mode:vertical-rl]">
        <span>{formatMoney(trip.pricePerPerson, language)}</span>
        <span className="sr-only">{t('trips.perPerson')}</span>
      </p>

      <div className="p-5">
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs font-medium text-white/85">
          <span className="inline-flex items-center gap-1">
            <MapPin aria-hidden="true" className="h-3.5 w-3.5 text-dusk" />
            {place}
          </span>
          <span className="inline-flex items-center gap-1">
            <CalendarDays aria-hidden="true" className="h-3.5 w-3.5 text-dusk" />
            {formatDateRange(trip.startDate, trip.endDate, language)} ·{' '}
            {t('trips.days', { count: days, n: formatCount(days, language) })}
          </span>
        </p>

        <h3 className="mt-2 text-xl font-semibold leading-snug text-white!">
          {/* The whole card is clickable through this link's stretched overlay. */}
          <Link to={`/trips/${id}`} className="after:absolute after:inset-0 focus:outline-none">
            {trip.title}
          </Link>
        </h3>

        <span
          aria-hidden="true"
          className="mt-3 block h-px w-12 origin-left bg-dusk transition-all duration-700 group-hover:w-full"
        />

        <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
          <p className="flex min-w-0 flex-wrap items-center gap-2 text-sm text-white/80">
            <span className="truncate">
              {t('trips.hostedBy', { name: trip.hostName ?? t('trips.aHost') })}
            </span>
            <VerificationBadge level={trip.hostVerifiedLevel} />
          </p>
          <SeatsBadge seatsLeft={seatsLeft} onPhoto />
        </div>
      </div>
    </article>
  );
}

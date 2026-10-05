import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { asNumber } from '@/api/client';
import { Scenery } from '@/components/Scenery';
import { formatCount, formatMoney, toLanguage } from '@/lib/format';
import { StatusBadge } from './TripBadges';
import type { DestinationSummary } from './tripsApi';

/** A destination tile: the landscape, its safety status, and what is on offer there. */
export function DestinationCard({ destination }: { destination: DestinationSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const name = language === 'bn' ? destination.nameBn : destination.name;
  const division = language === 'bn' ? destination.divisionBn : destination.division;
  const upcoming = asNumber(destination.upcomingTrips);

  return (
    <article className="group relative overflow-hidden rounded-3xl bg-deep shadow-sm transition hover:-translate-y-1 hover:shadow-xl">
      <Scenery
        kind={destination.kind}
        className="h-56 w-full transition duration-500 group-hover:scale-105"
      />
      <div className="absolute inset-0 bg-linear-to-t from-deep/90 via-deep/30 to-transparent" />

      <div className="absolute left-3 top-3">
        <StatusBadge status={destination.status} />
      </div>

      <div className="absolute inset-x-0 bottom-0 p-4 text-white">
        <p className="text-xs text-white/75">
          {t(`kind.${destination.kind}`)} · {t('destination.division', { division })}
        </p>
        <h3 className="text-xl font-bold">
          <Link
            to={`/destinations/${destination.slug}`}
            className="after:absolute after:inset-0 focus:outline-none"
          >
            {name}
          </Link>
        </h3>
        <p className="mt-0.5 text-sm text-white/85">
          {upcoming > 0
            ? t('destination.upcoming', { count: upcoming, n: formatCount(upcoming, language) })
            : t('destination.noTrips')}
          {destination.fromPrice !== null && (
            <>
              {' · '}
              <span className="whitespace-nowrap font-semibold text-dusk">
                {t('destination.from', { price: formatMoney(destination.fromPrice, language) })}
              </span>
            </>
          )}
        </p>
      </div>
    </article>
  );
}

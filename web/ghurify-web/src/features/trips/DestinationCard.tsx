import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowRight, MapPin } from 'lucide-react';
import { asNumber } from '@/api/client';
import { Photo } from '@/components/ui/Photo';
import { formatCount, formatMoney, toLanguage } from '@/lib/format';
import { StatusBadge } from './TripBadges';
import type { DestinationSummary } from './tripsApi';

/**
 * A destination as a tall photo tile: its live safety status, how many trips are coming up,
 * and where it is. On hover (or keyboard focus) the "from" price and an explore cue rise in;
 * on touch screens, which cannot hover, they are always shown.
 */
export function DestinationCard({ destination }: { destination: DestinationSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const name = language === 'bn' ? destination.nameBn : destination.name;
  const division = language === 'bn' ? destination.divisionBn : destination.division;
  const upcoming = asNumber(destination.upcomingTrips);

  return (
    <article className="group relative isolate flex aspect-3/4 flex-col justify-end overflow-hidden rounded-2xl bg-night text-white shadow-sm transition duration-500 hover:-translate-y-1 hover:shadow-2xl focus-within:ring-4 focus-within:ring-turmeric/60">
      <Photo
        slug={destination.slug}
        kind={destination.kind}
        cut="card"
        decorative
        className="absolute! inset-0 -z-10"
        imgClassName="photo-zoom"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-t from-night/95 via-night/30 to-transparent"
      />

      <div className="absolute left-4 top-4">
        <StatusBadge status={destination.status} />
      </div>

      {upcoming > 0 && (
        <p className="absolute right-4 top-0 rotate-180 rounded-t-md bg-hill px-2 py-3 text-xs font-semibold text-white shadow-md [writing-mode:vertical-rl]">
          {t('destination.upcoming', { count: upcoming, n: formatCount(upcoming, language) })}
        </p>
      )}

      <div className="p-5">
        <p className="text-xs font-medium text-white/75">
          {t(`kind.${destination.kind}`)} · {t('destination.division', { division })}
        </p>
        <h3 className="mt-1 flex items-center gap-1.5 text-xl font-semibold text-white!">
          <MapPin aria-hidden="true" className="h-5 w-5 shrink-0 text-dusk" />
          <Link
            to={`/destinations/${destination.slug}`}
            className="after:absolute after:inset-0 focus:outline-none"
          >
            {name}
          </Link>
        </h3>

        <span
          aria-hidden="true"
          className="mt-3 block h-px w-10 bg-dusk transition-all duration-700 group-hover:w-full"
        />

        <div className="grid transition-all duration-500 ease-out [@media(hover:hover)]:grid-rows-[0fr] [@media(hover:hover)]:opacity-0 [@media(hover:hover)]:group-focus-within:grid-rows-[1fr] [@media(hover:hover)]:group-focus-within:opacity-100 [@media(hover:hover)]:group-hover:grid-rows-[1fr] [@media(hover:hover)]:group-hover:opacity-100">
          <div className="min-h-0 overflow-hidden">
            <p className="flex items-center justify-between gap-2 pt-3 text-sm">
              <span className="text-white/85">
                {upcoming === 0
                  ? t('destination.noTrips')
                  : destination.fromPrice !== null && (
                      <span className="font-semibold text-dusk">
                        {t('destination.from', {
                          price: formatMoney(destination.fromPrice, language),
                        })}
                      </span>
                    )}
              </span>
              <span className="inline-flex items-center gap-1 font-medium">
                {t('destination.explore')}
                <ArrowRight
                  aria-hidden="true"
                  className="h-4 w-4 transition-transform group-hover:translate-x-1"
                />
              </span>
            </p>
          </div>
        </div>
      </div>
    </article>
  );
}

import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { MapPinned } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass } from '@/components/Field';
import { Photo } from '@/components/ui/Photo';
import { formatCount, formatDateRange, formatFullDate, toLanguage } from '@/lib/format';
import { DistrictMap, type DistrictPin } from './DistrictMap';
import { districtCount, placesByDistrict } from './districts';
import { defaultTheme } from './mapThemes';
import { pointOf } from './travelApi';
import { useSharedTravelMap } from './useTravelMap';

/**
 * Someone's travel map on their public profile, when they share it: the Ghurify destinations they
 * have been to, their districts filled in on Bangladesh, with when and on which trip. Nothing at all
 * when they do not share it. Light on purpose: a drawing of the country, not map tiles.
 */
export function SharedTravelMap({ userId, name }: { userId: number; name: string }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const map = useSharedTravelMap(userId);
  const [selected, setSelected] = useState<string | null>(null);
  const places = useMemo(() => map.data?.places ?? [], [map.data]);
  const byDistrict = useMemo(() => placesByDistrict(places), [places]);

  if (!map.data?.shared || map.data.places.length === 0) {
    return null;
  }

  const { summary } = map.data;
  const chosen = places.find((place) => place.key === selected) ?? places[0]!;
  const placeName = (place: (typeof places)[number]) =>
    language === 'bn' && place.nameBn ? place.nameBn : place.name;
  const pins: DistrictPin[] = places.flatMap((place) => {
    const point = pointOf(place);
    return point
      ? [
          {
            key: place.key,
            latitude: point[0],
            longitude: point[1],
            tone: place.visits.some((visit) => visit.source === 'Trip') ? 'trip' : 'added',
            name: placeName(place),
          } satisfies DistrictPin,
        ]
      : [];
  });
  return (
    <section aria-labelledby="travel-map-title" className={cardClass}>
      <h2 id="travel-map-title" className="flex items-center gap-2 text-xl font-semibold">
        <MapPinned aria-hidden="true" className="h-5 w-5 text-hill" />
        {t('travel.public.title', { name })}
      </h2>
      <p className="mt-1 text-sm text-deep/70">
        {t('travel.public.summary', {
          places: formatCount(summary.places, language),
          divisions: formatCount(summary.divisions, language),
          trips: formatCount(summary.trips, language),
          count: asNumber(summary.places),
        })}
      </p>

      <div className="mt-5 grid items-start gap-5 sm:grid-cols-[minmax(0,17rem)_1fr]">
        <div className="rounded-3xl p-3" style={{ background: defaultTheme.paper }}>
          <p className="px-2 pt-1 font-display text-sm font-semibold text-deep">
            <span className="text-2xl font-extrabold text-hill">
              {formatCount(byDistrict.size, language)}
            </span>
            <span className="text-deep/50">/{formatCount(districtCount, language)}</span>{' '}
            {t('travel.summary.districts')}
          </p>
          <DistrictMap
            theme={defaultTheme}
            visited={new Map([...byDistrict].map(([slug, items]) => [slug, items.length]))}
            pins={pins}
            selectedPin={chosen.key}
            showLabels={false}
            onSelectPin={setSelected}
            className="mx-auto w-full max-w-64"
          />
        </div>

        <div className="flex min-w-0 flex-col gap-4">
          <ul className="flex flex-wrap gap-2" aria-label={t('travel.public.places')}>
            {places.map((place) => (
              <li key={place.key}>
                <button
                  type="button"
                  aria-pressed={place.key === chosen.key}
                  onClick={() => setSelected(place.key)}
                  className={`inline-flex items-center gap-2 rounded-full py-1 pl-1 pr-3 text-sm font-semibold transition ${
                    place.key === chosen.key
                      ? 'bg-hill text-white shadow'
                      : 'bg-mist text-deep hover:bg-hill/10'
                  }`}
                >
                  {place.destinationSlug && (
                    <Photo
                      slug={place.destinationSlug}
                      kind={place.kind ?? 'Hills'}
                      cut="card"
                      decorative
                      className="h-7 w-7 rounded-full"
                    />
                  )}
                  {placeName(place)}
                </button>
              </li>
            ))}
          </ul>

          <div className="rounded-2xl bg-mist/70 p-4" aria-live="polite">
            <p className="font-display text-lg font-semibold text-deep">
              {chosen.destinationSlug ? (
                <Link
                  to={`/destinations/${chosen.destinationSlug}`}
                  className="hover:text-hill hover:underline"
                >
                  {placeName(chosen)}
                </Link>
              ) : (
                placeName(chosen)
              )}
            </p>
            <ul className="mt-2 flex flex-col gap-1.5 text-sm text-deep/75">
              {chosen.visits.map((visit) => (
                <li key={String(visit.id)}>
                  {visit.trip ? (
                    <>
                      {formatDateRange(visit.trip.startDate, visit.trip.endDate, language)}{' '}
                      {visit.trip.startDate.slice(0, 4)} ·{' '}
                      <Link
                        to={`/trips/${asNumber(visit.trip.id)}`}
                        className="font-semibold text-hill hover:underline"
                      >
                        {visit.trip.title}
                      </Link>
                      {visit.trip.asHost && ` · ${t('travel.public.hosted')}`}
                    </>
                  ) : (
                    formatFullDate(visit.visitedOn, language)
                  )}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}

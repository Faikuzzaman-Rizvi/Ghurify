import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { MapContainer, TileLayer, CircleMarker, Tooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { ApiError, asNumber } from '@/api/client';
import { Scenery } from '@/components/Scenery';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { toLanguage } from '@/lib/format';
import { StatusBadge } from './TripBadges';
import { TripCard } from './TripCard';
import type { DestinationSummary } from './tripsApi';
import { useDestination, useTripSearch } from './useTrips';

/** A destination: what it is, where it is, its safety status, and the trips going there. */
export function DestinationPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams();
  const { data, isPending, isError, error, refetch } = useDestination(slug);

  if (isError && error instanceof ApiError && error.status === 404) {
    return (
      <div className="mx-auto max-w-3xl px-4 pt-16">
        <EmptyState
          title={t('destination.notFound')}
          action={
            <Link
              to="/"
              className="mt-2 rounded-full bg-hill px-4 py-2 text-sm font-medium text-white"
            >
              {t('notFound.home')}
            </Link>
          }
        />
      </div>
    );
  }

  if (isPending) {
    return (
      <div role="status" className="mx-auto max-w-6xl px-4 pt-8">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-72 animate-pulse rounded-4xl bg-hill/10" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-3xl px-4 pt-16">
        <ErrorState message={t('common.error')} onRetry={() => void refetch()} />
      </div>
    );
  }

  return <DestinationView destination={data} />;
}

function DestinationView({ destination }: { destination: DestinationSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const name = language === 'bn' ? destination.nameBn : destination.name;
  const summary = language === 'bn' ? destination.summaryBn : destination.summary;
  const division = language === 'bn' ? destination.divisionBn : destination.division;
  const note = language === 'bn' ? destination.statusNoteBn : destination.statusNote;
  const trips = useTripSearch({ destination: destination.slug, pageSize: 12 });

  const latitude = destination.latitude === null ? null : asNumber(destination.latitude);
  const longitude = destination.longitude === null ? null : asNumber(destination.longitude);

  return (
    <article>
      <header className="relative isolate overflow-hidden">
        <Scenery kind={destination.kind} className="absolute inset-0 -z-10 h-full w-full" />
        <div className="absolute inset-0 -z-10 bg-linear-to-t from-deep/85 via-deep/25 to-transparent" />
        <div className="mx-auto max-w-6xl px-4 pb-10 pt-28 text-white sm:pt-40">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={destination.status} />
            <span className="rounded-full bg-white/20 px-3 py-0.5 text-xs font-semibold backdrop-blur">
              {t(`kind.${destination.kind}`)} · {t('destination.division', { division })}
            </span>
          </div>
          <h1 className="mt-3 text-4xl font-extrabold sm:text-6xl">{name}</h1>
          <p className="mt-2 max-w-2xl text-lg text-white/90">{summary}</p>
        </div>
      </header>

      <div className="mx-auto grid max-w-6xl gap-8 px-4 pt-8 lg:grid-cols-[1fr_380px]">
        <section>
          {destination.status !== 'Open' && note && (
            <aside
              role="note"
              className="mb-6 flex gap-3 rounded-2xl bg-amber-50 p-4 ring-1 ring-amber-600/30"
            >
              <span aria-hidden="true" className="text-xl">
                ⚠️
              </span>
              <div>
                <p className="font-semibold text-amber-900">{t('trip.caution', { place: name })}</p>
                <p className="text-sm text-amber-900/80">{note}</p>
              </div>
            </aside>
          )}

          <h2 className="text-2xl font-bold text-deep">
            {t('destination.tripsHere', { place: name })}
          </h2>
          <div className="mt-4 grid gap-6 sm:grid-cols-2">
            {trips.isPending ? (
              <CardSkeletons count={2} tall />
            ) : trips.isError ? (
              <ErrorState message={t('trips.loadError')} onRetry={() => void trips.refetch()} />
            ) : trips.data.items.length === 0 ? (
              <EmptyState title={t('destination.noTrips')} />
            ) : (
              trips.data.items.map((trip) => <TripCard key={String(trip.id)} trip={trip} />)
            )}
          </div>
        </section>

        {latitude !== null && longitude !== null && (
          <aside className="lg:sticky lg:top-24 lg:self-start">
            <div
              className="h-80 overflow-hidden rounded-3xl shadow-md ring-1 ring-hill/10"
              role="img"
              aria-label={t('destination.map', { place: name })}
            >
              <MapContainer
                center={[latitude, longitude]}
                zoom={9}
                scrollWheelZoom={false}
                className="h-full w-full"
              >
                <TileLayer
                  attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                  url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                />
                <CircleMarker
                  center={[latitude, longitude]}
                  radius={12}
                  pathOptions={{
                    color: '#173f2e',
                    fillColor: '#d99a12',
                    fillOpacity: 0.9,
                    weight: 3,
                  }}
                >
                  <Tooltip permanent direction="top" offset={[0, -12]}>
                    {name}
                  </Tooltip>
                </CircleMarker>
              </MapContainer>
            </div>
          </aside>
        )}
      </div>
    </article>
  );
}

import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { MapContainer, TileLayer, CircleMarker, Tooltip } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import {
  ArrowRight,
  Compass,
  Landmark,
  Tent,
  TriangleAlert,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { ApiError, asNumber } from '@/api/client';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { formatCount, formatMoney, toLanguage } from '@/lib/format';
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
      <div className="container-page max-w-3xl py-16">
        <EmptyState
          title={t('destination.notFound')}
          action={
            <Link
              to="/"
              className="mt-2 rounded-full bg-hill px-5 py-2.5 text-sm font-semibold text-white"
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
      <div role="status">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-120 animate-pulse bg-hill/15" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="container-page max-w-3xl py-16">
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
  const upcoming = asNumber(destination.upcomingTrips);

  const latitude = destination.latitude === null ? null : asNumber(destination.latitude);
  const longitude = destination.longitude === null ? null : asNumber(destination.longitude);

  const facts: { icon: LucideIcon; label: string; value: string }[] = [
    { icon: Compass, label: t('destination.kindLabel'), value: t(`kind.${destination.kind}`) },
    { icon: Landmark, label: t('destination.divisionLabel'), value: division },
    {
      icon: Tent,
      label: t('destination.upcomingLabel'),
      value: formatCount(upcoming, language),
    },
    ...(destination.fromPrice !== null
      ? [
          {
            icon: Wallet,
            label: t('destination.fromLabel'),
            value: formatMoney(destination.fromPrice, language),
          },
        ]
      : []),
  ];

  return (
    <article>
      <PageBanner
        slug={destination.slug}
        kind={destination.kind}
        tall
        eyebrow={`${t(`kind.${destination.kind}`)} · ${t('destination.division', { division })}`}
        title={name}
      >
        <div className="mt-4">
          <StatusBadge status={destination.status} />
        </div>
        <p className="mt-4 max-w-2xl text-lg text-white/90">{summary}</p>
      </PageBanner>

      <div className="container-page grid gap-10 py-12 lg:grid-cols-[1fr_22rem] lg:gap-12 lg:py-16">
        <section className="min-w-0">
          {destination.status !== 'Open' && note && (
            <aside
              role="note"
              className="mb-8 flex gap-3 rounded-2xl bg-amber-50 p-5 ring-1 ring-amber-600/30"
            >
              <TriangleAlert aria-hidden="true" className="h-6 w-6 shrink-0 text-amber-700" />
              <div>
                <p className="font-semibold text-amber-900">{t('trip.caution', { place: name })}</p>
                <p className="mt-1 text-sm text-amber-900/80">{note}</p>
              </div>
            </aside>
          )}

          <h2 className="text-2xl font-semibold sm:text-3xl">
            {t('destination.tripsHere', { place: name })}
          </h2>
          <div className="mt-6 grid gap-6 sm:grid-cols-2">
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

        <aside className="flex flex-col gap-5 lg:sticky lg:top-24 lg:self-start">
          {latitude !== null && longitude !== null && (
            <div
              className="h-72 overflow-hidden rounded-2xl shadow-md ring-1 ring-hill/10"
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
          )}

          <div className="overflow-hidden rounded-2xl bg-mist">
            <h2 className="bg-hill px-5 py-4 text-lg font-semibold text-white!">
              {t('destination.about', { place: name })}
            </h2>
            <dl className="divide-y divide-hill/10 px-5">
              {facts.map(({ icon: Icon, label, value }) => (
                <div key={label} className="flex items-center gap-3 py-3.5">
                  <Icon aria-hidden="true" className="h-5 w-5 shrink-0 text-hill" />
                  <dt className="flex-1 text-sm text-deep/60">{label}</dt>
                  <dd className="text-right font-semibold text-deep">{value}</dd>
                </div>
              ))}
            </dl>
            <div className="p-5 pt-2">
              <Link
                to={`/trips?destination=${destination.slug}`}
                className="flex items-center justify-center gap-2 rounded-full bg-turmeric px-5 py-3 font-semibold text-night transition hover:bg-dusk"
              >
                {t('destination.viewTrips')}
                <ArrowRight aria-hidden="true" className="h-4 w-4" />
              </Link>
            </div>
          </div>
        </aside>
      </div>
    </article>
  );
}

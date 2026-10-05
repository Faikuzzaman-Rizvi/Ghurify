import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Scenery } from '@/components/Scenery';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { DestinationCard } from '@/features/trips/DestinationCard';
import { TripCard } from '@/features/trips/TripCard';
import { TripSearchForm } from '@/features/trips/TripSearchForm';
import { useDestinations, useTripSearch } from '@/features/trips/useTrips';

/** The landing page: search first, then where you can go, what is leaving soon, and why it is safe. */
export function HomePage() {
  const { t } = useTranslation();

  return (
    <>
      <Hero />

      <PromiseStrip />

      <section id="destinations" className="mx-auto max-w-6xl scroll-mt-24 px-4 pt-20">
        <SectionHeading
          title={t('home.destinationsTitle')}
          subtitle={t('home.destinationsSubtitle')}
        />
        <div data-tour="destinations" className="mt-8">
          <Destinations />
        </div>
      </section>

      <section id="trips" className="mx-auto max-w-6xl scroll-mt-24 px-4 pt-20">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <SectionHeading title={t('home.tripsTitle')} subtitle={t('home.tripsSubtitle')} />
          <Link
            to="/trips"
            className="rounded-full border border-hill/30 px-4 py-2 text-sm font-medium text-hill transition hover:bg-hill hover:text-white"
          >
            {t('home.seeAll')} →
          </Link>
        </div>
        <div data-tour="trips" className="mt-8">
          <FeaturedTrips />
        </div>
      </section>

      <HowItWorks />

      <Safety />

      <CallToAction />
    </>
  );
}

function Hero() {
  const { t } = useTranslation();

  return (
    <section className="relative isolate overflow-hidden">
      <Scenery kind="Hills" animated className="absolute inset-0 -z-10 h-full w-full" />
      <div className="absolute inset-x-0 bottom-0 -z-10 h-1/2 bg-linear-to-t from-sand via-sand/40 to-transparent" />
      {/* Keeps the headline and subtitle readable where they cross the hills. */}
      <div className="absolute inset-y-0 left-0 -z-10 w-full bg-linear-to-r from-sand/85 via-sand/50 to-transparent sm:w-2/3" />

      <div className="mx-auto max-w-6xl px-4 pb-16 pt-14 sm:pb-24 sm:pt-20">
        <div className="max-w-2xl animate-rise">
          <p className="inline-flex items-center gap-2 rounded-full bg-white/80 px-3 py-1 text-sm font-medium text-hill shadow-sm">
            <span aria-hidden="true">🇧🇩</span>
            {t('home.eyebrow')}
          </p>
          <h1 className="mt-4 text-4xl font-extrabold text-deep sm:text-6xl">{t('home.title')}</h1>
          <p className="mt-4 max-w-xl text-lg text-deep/80">{t('home.subtitle')}</p>
        </div>

        <div
          data-tour="search"
          className="mt-8 max-w-4xl animate-rise rounded-3xl bg-white/95 p-4 shadow-xl ring-1 ring-hill/10 backdrop-blur sm:p-5"
        >
          <h2 className="mb-3 text-lg font-bold text-deep">{t('home.searchTitle')}</h2>
          <TripSearchForm />
        </div>
      </div>
    </section>
  );
}

function PromiseStrip() {
  const { t } = useTranslation();
  const items = [
    { icon: '🛡️', key: 'verified' },
    { icon: '💰', key: 'escrow' },
    { icon: '🆘', key: 'sos' },
    { icon: '👭', key: 'women' },
  ];

  return (
    <section aria-label={t('nav.safety')} className="bg-hill text-white">
      <ul className="mx-auto grid max-w-6xl grid-cols-2 gap-3 px-4 py-4 text-sm font-medium sm:grid-cols-4">
        {items.map((item) => (
          <li key={item.key} className="flex items-center gap-2">
            <span aria-hidden="true" className="text-lg">
              {item.icon}
            </span>
            {t(`home.promise.${item.key}`)}
          </li>
        ))}
      </ul>
    </section>
  );
}

function SectionHeading({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="max-w-2xl">
      <h2 className="text-3xl font-extrabold text-deep sm:text-4xl">{title}</h2>
      <p className="mt-2 text-deep/70">{subtitle}</p>
    </div>
  );
}

function Destinations() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch } = useDestinations();

  return (
    <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-5">
      {isPending ? (
        <CardSkeletons count={6} />
      ) : isError ? (
        <ErrorState message={t('common.error')} onRetry={() => void refetch()} />
      ) : data.length === 0 ? (
        <EmptyState title={t('common.empty')} />
      ) : (
        data.map((destination) => (
          <DestinationCard key={destination.slug} destination={destination} />
        ))
      )}
    </div>
  );
}

function FeaturedTrips() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch } = useTripSearch({ pageSize: 6 });

  return (
    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {isPending ? (
        <CardSkeletons count={3} tall />
      ) : isError ? (
        <ErrorState message={t('trips.loadError')} onRetry={() => void refetch()} />
      ) : data.items.length === 0 ? (
        <EmptyState title={t('trips.empty')} />
      ) : (
        data.items.map((trip) => <TripCard key={String(trip.id)} trip={trip} />)
      )}
    </div>
  );
}

function HowItWorks() {
  const { t } = useTranslation();
  const steps = [
    { icon: '🔎', key: 'find' },
    { icon: '🤝', key: 'request' },
    { icon: '🔐', key: 'pay' },
    { icon: '🏕️', key: 'travel' },
  ];

  return (
    <section className="mx-auto max-w-6xl px-4 pt-24">
      <h2 className="text-center text-3xl font-extrabold text-deep sm:text-4xl">
        {t('home.howTitle')}
      </h2>
      <ol className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {steps.map((step, index) => (
          <li
            key={step.key}
            className="relative rounded-3xl bg-white p-6 shadow-sm ring-1 ring-hill/10"
          >
            <span className="absolute -top-4 left-6 flex h-8 w-8 items-center justify-center rounded-full bg-turmeric font-display font-bold text-deep">
              {index + 1}
            </span>
            <span className="text-3xl" aria-hidden="true">
              {step.icon}
            </span>
            <h3 className="mt-3 text-lg font-bold text-deep">
              {t(`home.steps.${step.key}.title`)}
            </h3>
            <p className="mt-1 text-sm text-deep/70">{t(`home.steps.${step.key}.body`)}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}

function Safety() {
  const { t } = useTranslation();
  const items = [
    { icon: '🪪', key: 'verified' },
    { icon: '🔐', key: 'escrow' },
    { icon: '🆘', key: 'sos' },
    { icon: '👭', key: 'women' },
    { icon: '⛈️', key: 'alerts' },
  ];

  return (
    <section id="safety" className="mx-auto mt-24 max-w-6xl scroll-mt-24 px-4">
      <div
        data-tour="safety"
        className="relative overflow-hidden rounded-4xl bg-deep px-6 py-12 text-white sm:px-10"
      >
        <Scenery kind="Forest" className="absolute inset-0 z-0 h-full w-full opacity-15" />
        <div className="relative">
          <h2 className="text-3xl font-extrabold sm:text-4xl">{t('home.safetyTitle')}</h2>
          <p className="mt-2 max-w-2xl text-white/80">{t('home.safetySubtitle')}</p>
          <ul className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            {items.map((item) => (
              <li key={item.key} className="rounded-2xl bg-white/10 p-4 backdrop-blur">
                <span className="text-2xl" aria-hidden="true">
                  {item.icon}
                </span>
                <h3 className="mt-2 font-bold">{t(`home.safety.${item.key}.title`)}</h3>
                <p className="mt-1 text-sm text-white/75">{t(`home.safety.${item.key}.body`)}</p>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

function CallToAction() {
  const { t } = useTranslation();

  return (
    <section className="mx-auto mt-24 max-w-6xl px-4">
      <div className="relative overflow-hidden rounded-4xl bg-turmeric px-6 py-12 sm:px-10">
        <Scenery
          kind="Beach"
          className="absolute inset-y-0 right-0 hidden h-full w-1/2 opacity-90 sm:block"
        />
        <div className="relative max-w-lg">
          <h2 className="text-3xl font-extrabold text-deep">{t('home.ctaTitle')}</h2>
          <p className="mt-2 text-deep/80">{t('home.ctaBody')}</p>
          <Link
            to="/login"
            className="mt-6 inline-flex rounded-full bg-deep px-6 py-3 font-medium text-white transition hover:bg-hill"
          >
            {t('home.ctaButton')}
          </Link>
        </div>
      </div>
    </section>
  );
}

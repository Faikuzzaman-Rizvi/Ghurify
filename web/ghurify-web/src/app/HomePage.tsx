import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { preload } from 'react-dom';
import { Trans, useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowRight,
  BadgeCheck,
  CircleCheck,
  CloudLightning,
  HandCoins,
  LockKeyhole,
  MapPin,
  Mountain,
  Pause,
  Play,
  Search,
  Siren,
  Tent,
  UserRoundCheck,
  UsersRound,
  type LucideIcon,
} from 'lucide-react';
import { asNumber } from '@/api/client';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { useHeroUnderHeader } from '@/components/ui/headerStore';
import { Photo } from '@/components/ui/Photo';
import { Reveal } from '@/components/ui/Reveal';
import { SectionHeading } from '@/components/ui/SectionHeading';
import { DestinationCard } from '@/features/trips/DestinationCard';
import { TripCard } from '@/features/trips/TripCard';
import { TripSearchForm } from '@/features/trips/TripSearchForm';
import { useDestinations, useTripSearch } from '@/features/trips/useTrips';
import { formatCount, toLanguage } from '@/lib/format';
import { photoFor, photoSources } from '@/lib/photos';

/**
 * The landing page: a full-bleed photo hero with the search, then why Ghurify, where you can
 * go, what is leaving soon, how it works, and why it is safe.
 */
export function HomePage() {
  return (
    <>
      <Hero />
      <Why />
      <Destinations />
      <FeaturedTrips />
      <Numbers />
      <HowItWorks />
      <Safety />
      <CallToAction />
    </>
  );
}

/** The hero photos, in order. Kinds only matter if a photo fails and scenery stands in. */
const heroSlides = [
  { slug: 'sajek', kind: 'Hills' },
  { slug: 'saint-martins', kind: 'Island' },
  { slug: 'sylhet', kind: 'River' },
  { slug: 'tanguar-haor', kind: 'Wetland' },
] as const;

const slideMs = 7000;

function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' &&
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true
  );
}

/**
 * Full-screen hero: photos of real places cross-fade behind one fixed headline. It advances by
 * itself (never for readers who prefer reduced motion), pauses on hover and focus, and has
 * a pause button, as anything that moves on its own for more than five seconds must.
 */
function Hero() {
  const { t } = useTranslation();
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(prefersReducedMotion);
  const [hovering, setHovering] = useState(false);
  useHeroUnderHeader();

  // Fetch the first photo with the page rather than after the bundle has run.
  const first = photoSources(photoFor(heroSlides[0].slug, heroSlides[0].kind), 'wide');
  preload(first.src, {
    as: 'image',
    imageSrcSet: first.srcSet,
    imageSizes: '100vw',
    fetchPriority: 'high',
  });

  useEffect(() => {
    if (paused || hovering) return;
    const timer = window.setTimeout(
      () => setIndex((current) => (current + 1) % heroSlides.length),
      slideMs,
    );
    return () => window.clearTimeout(timer);
  }, [index, paused, hovering]);

  const slide = heroSlides[index] ?? heroSlides[0];

  return (
    <section
      aria-label={t('home.heroLabel')}
      onPointerEnter={() => setHovering(true)}
      onPointerLeave={() => setHovering(false)}
      onFocus={() => setHovering(true)}
      onBlur={() => setHovering(false)}
      className="relative isolate z-10 flex min-h-[44rem] flex-col bg-night text-white lg:min-h-svh"
    >
      {heroSlides.map((item, slideIndex) => (
        <div
          key={item.slug}
          aria-hidden={slideIndex !== index}
          className={`absolute inset-0 -z-10 transition-opacity duration-[1400ms] ease-in-out ${
            slideIndex === index ? 'opacity-100' : 'opacity-0'
          }`}
        >
          <Photo
            slug={item.slug}
            kind={item.kind}
            cut="wide"
            priority={slideIndex === 0}
            className="h-full w-full"
            imgClassName={slideIndex === index ? 'animate-ken-burns' : ''}
          />
        </div>
      ))}
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-b from-night/70 via-night/45 to-night/80"
      />

      <div className="container-page flex flex-1 flex-col items-center justify-center pb-10 pt-32 text-center">
        <p className="eyebrow animate-rise text-white!">{t('home.eyebrow')}</p>
        <h1 className="mt-5 max-w-5xl animate-rise text-4xl font-bold uppercase leading-[1.08] text-white! [animation-delay:120ms] sm:text-6xl lg:text-7xl">
          <Trans i18nKey="home.title" components={{ accent: <span className="hero-outline" /> }} />
        </h1>
        <p className="mt-6 max-w-2xl animate-rise text-base text-white/85 [animation-delay:240ms] sm:text-lg">
          {t('home.subtitle')}
        </p>

        <div
          data-tour="search"
          className="mt-10 w-full max-w-5xl animate-rise [animation-delay:360ms]"
        >
          <h2 className="sr-only">{t('home.searchTitle')}</h2>
          <TripSearchForm />
        </div>
      </div>

      <div className="container-page flex items-center justify-between gap-4 pb-8">
        <p className="flex items-center gap-1.5 text-sm text-white/80" aria-live="off">
          <MapPin aria-hidden="true" className="h-4 w-4 text-dusk" />
          {t(`home.heroPlace.${slide.slug}`)}
        </p>

        <div className="flex items-center gap-3">
          <ol className="flex items-center gap-1">
            {heroSlides.map((item, slideIndex) => (
              <li key={item.slug}>
                <button
                  type="button"
                  onClick={() => setIndex(slideIndex)}
                  aria-label={t('home.showSlide', { place: t(`home.heroPlace.${item.slug}`) })}
                  aria-current={slideIndex === index}
                  className="group/dot flex h-6 min-w-6 items-center justify-center"
                >
                  {/* The dot is small; the button around it is a full 24px touch target. */}
                  <span
                    className={`block h-3 rounded-full border-2 border-white transition-all ${
                      slideIndex === index ? 'w-8 bg-white' : 'w-3 group-hover/dot:bg-white/50'
                    }`}
                  />
                </button>
              </li>
            ))}
          </ol>
          <button
            type="button"
            onClick={() => setPaused((value) => !value)}
            aria-label={paused ? t('home.playSlides') : t('home.pauseSlides')}
            className="rounded-full border border-white/40 p-1.5 transition hover:bg-white/15"
          >
            {paused ? (
              <Play aria-hidden="true" className="h-3.5 w-3.5" />
            ) : (
              <Pause aria-hidden="true" className="h-3.5 w-3.5" />
            )}
          </button>
        </div>
      </div>
    </section>
  );
}

/** Why Ghurify: the promise in words, beside a photo with a slowly turning badge. */
function Why() {
  const { t } = useTranslation();
  const points: { key: string; icon: LucideIcon }[] = [
    { key: 'verified', icon: BadgeCheck },
    { key: 'escrow', icon: LockKeyhole },
    { key: 'sos', icon: Siren },
    { key: 'women', icon: UsersRound },
  ];

  return (
    <section className="section overflow-hidden">
      <div className="container-page grid items-center gap-14 lg:grid-cols-2 lg:gap-20">
        <Reveal>
          <SectionHeading
            eyebrow={t('home.why.eyebrow')}
            titleKey="home.why.title"
            subtitle={t('home.why.body')}
          />
          <ul className="mt-8 grid gap-4 sm:grid-cols-2">
            {points.map(({ key, icon: Icon }) => (
              <li key={key} className="flex items-center gap-3 font-medium text-deep">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-hill/10 text-hill">
                  <Icon aria-hidden="true" className="h-5 w-5" />
                </span>
                {t(`home.promise.${key}`)}
              </li>
            ))}
          </ul>
          <div className="mt-10 flex flex-wrap items-center gap-4">
            <Link
              to="/trips"
              className="inline-flex items-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white transition hover:bg-deep"
            >
              {t('home.seeAll')}
              <ArrowRight aria-hidden="true" className="h-4 w-4" />
            </Link>
            <Link
              to="/#safety"
              className="font-semibold text-hill underline-offset-4 hover:underline"
            >
              {t('home.why.howSafe')}
            </Link>
          </div>
        </Reveal>

        <Reveal delay={150} className="relative mx-auto w-full max-w-md lg:max-w-none">
          {/* A turmeric block offset behind the photo, and a dotted texture above it. */}
          <div
            aria-hidden="true"
            className="absolute -bottom-5 -right-5 top-10 left-10 rounded-2xl bg-turmeric"
          />
          <div
            aria-hidden="true"
            className="absolute -left-6 -top-6 h-28 w-28 bg-[radial-gradient(var(--color-hill)_1.5px,transparent_1.5px)] bg-size-[12px_12px] opacity-30"
          />
          <Photo
            slug="rangamati"
            kind="Lake"
            cut="card"
            sizes="(min-width: 1024px) 40vw, 90vw"
            className="relative aspect-4/5 rounded-2xl shadow-2xl lg:aspect-5/6"
          />
          <RotatingBadge />
        </Reveal>
      </div>
    </section>
  );
}

/** Length of the badge's text path: the circumference of a circle of radius 35, less a gap. */
const badgePathLength = 212;

/** A circle of text turning around the Ghurify sun: decorative, so hidden from readers. */
function RotatingBadge() {
  const { t, i18n } = useTranslation();
  const textRef = useRef<SVGTextElement>(null);

  // Scale the type so the words fill the circle. Stretching the spacing instead would pull
  // Bangla letters apart from their vowel signs.
  useLayoutEffect(() => {
    const text = textRef.current;
    if (!text || typeof text.getComputedTextLength !== 'function') return;
    text.style.fontSize = '8px';
    const length = text.getComputedTextLength();
    if (length > 0) {
      const size = Math.min(12, Math.max(6, (8 * badgePathLength) / length));
      text.style.fontSize = `${size}px`;
    }
  }, [i18n.language]);

  return (
    <div
      aria-hidden="true"
      className="absolute -bottom-8 -left-6 flex h-32 w-32 items-center justify-center rounded-full bg-white shadow-xl sm:h-36 sm:w-36"
    >
      <svg viewBox="0 0 100 100" className="absolute inset-0 h-full w-full animate-spin-slow">
        <defs>
          <path id="badge-circle" d="M50,50 m-35,0 a35,35 0 1,1 70,0 a35,35 0 1,1 -70,0" />
        </defs>
        <text ref={textRef} className="fill-deep font-display text-[8px] font-semibold uppercase">
          <textPath href="#badge-circle">{t('home.why.badge')}</textPath>
        </text>
      </svg>
      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-hill text-turmeric">
        <Mountain className="h-6 w-6" />
      </span>
    </div>
  );
}

function Destinations() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch } = useDestinations();

  return (
    <section id="destinations" className="section scroll-mt-16 bg-mist">
      <div className="container-page">
        <Reveal>
          <SectionHeading
            eyebrow={t('home.destinationsEyebrow')}
            titleKey="home.destinationsTitle"
            subtitle={t('home.destinationsSubtitle')}
          />
        </Reveal>

        {/* A swipeable row on phones, a grid from tablets up. */}
        <div
          data-tour="destinations"
          className="-mx-4 mt-10 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pb-4 scrollbar-none sm:mx-0 sm:grid sm:grid-cols-3 sm:gap-6 sm:overflow-visible sm:px-0 sm:pb-0 lg:grid-cols-5"
        >
          {isPending ? (
            <CardSkeletons
              count={5}
              tall
              itemClassName="w-[72vw] max-w-72 shrink-0 sm:w-auto sm:max-w-none"
            />
          ) : isError ? (
            <ErrorState message={t('common.error')} onRetry={() => void refetch()} />
          ) : data.length === 0 ? (
            <EmptyState title={t('common.empty')} />
          ) : (
            data.map((destination) => (
              <div
                key={destination.slug}
                className="w-[72vw] max-w-72 shrink-0 snap-start sm:w-auto sm:max-w-none"
              >
                <DestinationCard destination={destination} />
              </div>
            ))
          )}
        </div>
      </div>
    </section>
  );
}

function FeaturedTrips() {
  const { t } = useTranslation();
  const { data, isPending, isError, refetch } = useTripSearch({ pageSize: 6 });

  return (
    <section id="trips" className="section scroll-mt-16">
      <div className="container-page">
        <Reveal>
          <SectionHeading
            eyebrow={t('home.tripsEyebrow')}
            titleKey="home.tripsTitle"
            subtitle={t('home.tripsSubtitle')}
            action={
              <Link
                to="/trips"
                className="inline-flex items-center gap-2 rounded-full border-2 border-hill px-5 py-2.5 text-sm font-semibold text-hill transition hover:bg-hill hover:text-white"
              >
                {t('home.seeAll')}
                <ArrowRight aria-hidden="true" className="h-4 w-4" />
              </Link>
            }
          />
        </Reveal>
        <div data-tour="trips" className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {isPending ? (
            <CardSkeletons count={3} tall />
          ) : isError ? (
            <ErrorState message={t('trips.loadError')} onRetry={() => void refetch()} />
          ) : data.items.length === 0 ? (
            <EmptyState title={t('trips.empty')} />
          ) : (
            data.items.map((trip, index) => (
              <Reveal key={String(trip.id)} delay={(index % 3) * 100}>
                <TripCard trip={trip} />
              </Reveal>
            ))
          )}
        </div>
      </div>
    </section>
  );
}

/**
 * Real figures only: destination and trip counts come from the API, the other two are
 * product facts. Nothing here is a made-up marketing number.
 */
function Numbers() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data: destinations } = useDestinations();
  const { data: trips } = useTripSearch({ pageSize: 6 });

  const figures: { key: string; icon: LucideIcon; value: string | undefined }[] = [
    {
      key: 'destinations',
      icon: MapPin,
      value: destinations ? formatCount(destinations.length, language) : undefined,
    },
    {
      key: 'trips',
      icon: Tent,
      value: trips ? formatCount(asNumber(trips.totalCount), language) : undefined,
    },
    { key: 'escrow', icon: HandCoins, value: `${formatCount(100, language)}%` },
    { key: 'sos', icon: Siren, value: t('home.numbers.sosValue') },
  ];

  return (
    <section aria-label={t('home.numbers.label')} className="relative isolate overflow-hidden">
      <Photo
        slug="bandarban"
        kind="Hills"
        cut="wide"
        decorative
        className="absolute! inset-0 -z-10"
      />
      <div aria-hidden="true" className="absolute inset-0 -z-10 bg-deep/85" />
      <ul className="container-page grid grid-cols-2 gap-y-12 py-20 text-center text-white lg:grid-cols-4">
        {figures.map(({ key, icon: Icon, value }, index) => (
          <Reveal as="li" key={key} delay={index * 100}>
            <span className="mx-auto flex h-16 w-16 items-center justify-center rounded-full border border-white/25 bg-white/10 text-dusk">
              <Icon aria-hidden="true" className="h-7 w-7" />
            </span>
            <span className="mt-4 block font-display text-4xl font-bold text-white sm:text-5xl">
              {value ?? '—'}
            </span>
            <span className="mt-1 block text-sm font-medium text-white/80">
              {t(`home.numbers.${key}`)}
            </span>
          </Reveal>
        ))}
      </ul>
    </section>
  );
}

function HowItWorks() {
  const { t } = useTranslation();
  const steps: { key: string; icon: LucideIcon }[] = [
    { key: 'find', icon: Search },
    { key: 'request', icon: UserRoundCheck },
    { key: 'pay', icon: LockKeyhole },
    { key: 'travel', icon: Mountain },
  ];

  return (
    <section className="section">
      <div className="container-page">
        <Reveal>
          <SectionHeading align="center" eyebrow={t('home.howEyebrow')} titleKey="home.howTitle" />
        </Reveal>
        <ol className="mt-14 grid gap-10 sm:grid-cols-2 lg:grid-cols-4 lg:gap-6">
          {steps.map(({ key, icon: Icon }, index) => (
            <Reveal as="li" key={key} delay={index * 100} className="relative text-center">
              {/* A dashed line joining the steps on wide screens. */}
              {index < steps.length - 1 && (
                <span
                  aria-hidden="true"
                  className="absolute left-[calc(50%+3rem)] right-[calc(-50%+3rem)] top-10 hidden border-t-2 border-dashed border-hill/20 lg:block"
                />
              )}
              <span className="relative mx-auto flex h-20 w-20 items-center justify-center rounded-full bg-mist text-hill ring-8 ring-white">
                <Icon aria-hidden="true" className="h-8 w-8" />
                <span className="absolute -right-1 -top-1 flex h-7 w-7 items-center justify-center rounded-full bg-turmeric font-display text-sm font-bold text-night">
                  {index + 1}
                </span>
              </span>
              <h3 className="mt-5 text-lg font-semibold">{t(`home.steps.${key}.title`)}</h3>
              <p className="mx-auto mt-2 max-w-xs text-sm leading-relaxed">
                {t(`home.steps.${key}.body`)}
              </p>
            </Reveal>
          ))}
        </ol>
      </div>
    </section>
  );
}

function Safety() {
  const { t } = useTranslation();
  const items: { key: string; icon: LucideIcon }[] = [
    { key: 'verified', icon: BadgeCheck },
    { key: 'escrow', icon: LockKeyhole },
    { key: 'sos', icon: Siren },
    { key: 'women', icon: UsersRound },
    { key: 'alerts', icon: CloudLightning },
  ];

  return (
    <section id="safety" className="section scroll-mt-16 bg-night">
      <div data-tour="safety" className="container-page">
        <Reveal>
          <SectionHeading
            tone="dark"
            eyebrow={t('home.safetyEyebrow')}
            titleKey="home.safetyTitle"
            subtitle={t('home.safetySubtitle')}
          />
        </Reveal>
        <ul className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-5">
          {items.map(({ key, icon: Icon }, index) => (
            <Reveal
              as="li"
              key={key}
              delay={index * 80}
              className="group rounded-2xl border border-white/10 bg-white/5 p-6 transition hover:-translate-y-1 hover:border-dusk/40 hover:bg-white/10"
            >
              <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-turmeric text-night transition group-hover:scale-110">
                <Icon aria-hidden="true" className="h-6 w-6" />
              </span>
              <h3 className="mt-5 text-lg font-semibold text-white!">
                {t(`home.safety.${key}.title`)}
              </h3>
              <p className="mt-2 text-sm leading-relaxed text-white/70">
                {t(`home.safety.${key}.body`)}
              </p>
            </Reveal>
          ))}
        </ul>
      </div>
    </section>
  );
}

function CallToAction() {
  const { t } = useTranslation();

  return (
    <section className="relative isolate overflow-hidden">
      <Photo
        slug="kuakata"
        kind="Beach"
        cut="wide"
        decorative
        className="absolute! inset-0 -z-10"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-r from-night/90 via-night/70 to-night/20"
      />
      <div className="container-page py-24 sm:py-32">
        <Reveal className="max-w-xl text-white">
          <p className="eyebrow text-dusk!">{t('home.ctaEyebrow')}</p>
          <h2 className="mt-3 text-3xl font-bold text-white! sm:text-5xl">{t('home.ctaTitle')}</h2>
          <p className="mt-4 text-lg text-white/85">{t('home.ctaBody')}</p>
          <ul className="mt-6 space-y-2 text-white/90">
            {(['escrow', 'verified'] as const).map((key) => (
              <li key={key} className="flex items-center gap-2">
                <CircleCheck aria-hidden="true" className="h-5 w-5 text-dusk" />
                {t(`home.promise.${key}`)}
              </li>
            ))}
          </ul>
          <Link
            to="/login"
            className="mt-8 inline-flex items-center gap-2 rounded-full bg-turmeric px-7 py-3.5 font-semibold text-night shadow-lg transition hover:bg-dusk"
          >
            {t('home.ctaButton')}
            <ArrowRight aria-hidden="true" className="h-4 w-4" />
          </Link>
        </Reveal>
      </div>
    </section>
  );
}

import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import {
  ArrowLeft,
  CalendarDays,
  ChevronRight,
  Clock,
  Flag,
  LockKeyhole,
  MapPin,
  ReceiptText,
  ShieldCheck,
  TriangleAlert,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { ApiError, asNumber } from '@/api/client';
import { EmptyState, ErrorState } from '@/components/States';
import { Accordion } from '@/components/ui/Accordion';
import { PageBanner } from '@/components/ui/PageBanner';
import { Avatar } from '@/components/Avatar';
import { VerificationBadge } from '@/components/VerificationBadge';
import { JoinRequestDialog } from '@/features/bookings/JoinRequestDialog';
import { useAuthStore } from '@/features/auth/authStore';
import { ReportDialog } from '@/features/safety/ReportDialog';
import {
  formatCount,
  formatDate,
  formatDateRange,
  formatMoney,
  formatMonthYear,
  toLanguage,
  tripDays,
  type Language,
} from '@/lib/format';
import { GroupBadge, StatusBadge } from './TripBadges';
import type { CostCategory, TripDetail } from './tripsApi';
import { useTrip } from './useTrips';

const costColour: Record<CostCategory, string> = {
  Transport: 'bg-river',
  Stay: 'bg-hill',
  Food: 'bg-turmeric',
  Fees: 'bg-jamdani',
  Guide: 'bg-deep',
  Buffer: 'bg-dusk',
};

const difficultyStyle = {
  Easy: 'bg-emerald-50 text-emerald-800',
  Moderate: 'bg-amber-50 text-amber-800',
  Challenging: 'bg-red-50 text-red-800',
} as const;

/** One trip's page: everything a traveller needs to decide, before they commit. */
export function TripDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const tripId = Number(id);
  const { data: trip, isPending, isError, error, refetch } = useTrip(tripId);

  if (!Number.isFinite(tripId) || (isError && error instanceof ApiError && error.status === 404)) {
    return (
      <div className="container-page max-w-3xl py-16">
        <EmptyState
          title={t('trip.notFound')}
          action={
            <Link
              to="/trips"
              className="mt-2 rounded-full bg-hill px-5 py-2.5 text-sm font-semibold text-white"
            >
              {t('trip.back')}
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
        <div
          aria-hidden="true"
          className="container-page grid gap-8 py-10 lg:grid-cols-[1fr_22rem]"
        >
          <div className="h-96 animate-pulse rounded-2xl bg-hill/10" />
          <div className="h-80 animate-pulse rounded-2xl bg-hill/10" />
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="container-page max-w-3xl py-16">
        <ErrorState message={t('trips.loadError')} onRetry={() => void refetch()} />
      </div>
    );
  }

  return <TripView trip={trip} />;
}

function TripView({ trip }: { trip: TripDetail }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const place = language === 'bn' ? trip.destination.nameBn : trip.destination.name;
  const days = tripDays(trip.startDate, trip.endDate);
  const seats = asNumber(trip.seats);
  const seatsLeft = asNumber(trip.seatsLeft);
  const cautionNote =
    language === 'bn' ? trip.destination.statusNoteBn : trip.destination.statusNote;

  const facts: { icon: LucideIcon; label: string; value: string }[] = [
    {
      icon: Clock,
      label: t('trip.duration'),
      value: t('trips.days', { count: days, n: formatCount(days, language) }),
    },
    {
      icon: CalendarDays,
      label: t('trip.dates'),
      value: formatDateRange(trip.startDate, trip.endDate, language),
    },
    { icon: Users, label: t('trip.group'), value: t(`groupType.${trip.groupType}`) },
    { icon: MapPin, label: t('trip.meetingPoint'), value: trip.meetingPoint },
  ];

  return (
    <article className="pb-24 lg:pb-0">
      <PageBanner
        slug={trip.destination.slug}
        kind={trip.destination.kind}
        tall
        eyebrow={
          <nav aria-label={t('trip.breadcrumb')} className="flex items-center gap-1.5">
            <Link to="/trips" className="transition hover:text-white">
              {t('nav.explore')}
            </Link>
            <ChevronRight aria-hidden="true" className="h-3.5 w-3.5" />
            <Link
              to={`/destinations/${trip.destination.slug}`}
              className="transition hover:text-white"
            >
              {place}
            </Link>
          </nav>
        }
        title={trip.title}
      >
        <div className="mt-5 flex flex-wrap items-center gap-2">
          <GroupBadge groupType={trip.groupType} />
          <StatusBadge status={trip.destination.status} />
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-white backdrop-blur">
            <Users aria-hidden="true" className="h-3.5 w-3.5" />
            {seatsLeft > 0
              ? t('trips.seatsLeft', { count: seatsLeft, n: formatCount(seatsLeft, language) })
              : t('trips.full')}
          </span>
        </div>
      </PageBanner>

      <div className="container-page">
        {/* The key facts, overlapping the foot of the banner. */}
        <dl className="relative z-10 -mt-10 grid grid-cols-2 gap-px overflow-hidden rounded-2xl bg-mist shadow-xl ring-1 ring-hill/10 lg:mr-100 lg:grid-cols-4">
          {facts.map(({ icon: Icon, label, value }) => (
            <div key={label} className="flex items-center gap-3 bg-white p-4 sm:p-5">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-mist text-hill">
                <Icon aria-hidden="true" className="h-5 w-5" />
              </span>
              <div className="min-w-0">
                <dt className="text-xs text-deep/60">{label}</dt>
                <dd
                  className="line-clamp-2 font-display text-sm font-semibold leading-snug text-deep"
                  title={value}
                >
                  {value}
                </dd>
              </div>
            </div>
          ))}
        </dl>

        <div className="grid gap-10 py-10 lg:grid-cols-[1fr_22rem] lg:gap-12 lg:py-14">
          <div className="flex min-w-0 flex-col gap-12">
            {trip.destination.status !== 'Open' && cautionNote && (
              <aside
                role="note"
                className="flex gap-3 rounded-2xl bg-amber-50 p-5 ring-1 ring-amber-600/30"
              >
                <TriangleAlert aria-hidden="true" className="h-6 w-6 shrink-0 text-amber-700" />
                <div>
                  <p className="font-semibold text-amber-900">{t('trip.caution', { place })}</p>
                  <p className="mt-1 text-sm text-amber-900/80">{cautionNote}</p>
                </div>
              </aside>
            )}

            <DetailSection title={t('trip.overview')}>
              <p className="text-base leading-relaxed sm:text-lg">{trip.summary}</p>
            </DetailSection>

            <DetailSection title={t('trip.itinerary')}>
              <Accordion
                defaultOpen={['1']}
                toggleAllLabels={{ expand: t('trip.expandAll'), collapse: t('trip.collapseAll') }}
                items={trip.itinerary.map((day) => {
                  const dayNo = asNumber(day.dayNo);
                  return {
                    id: String(dayNo),
                    title: (
                      <span className="flex items-center gap-3">
                        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-turmeric text-sm font-bold text-night">
                          {formatCount(dayNo, language)}
                        </span>
                        <span>
                          <span className="sr-only">
                            {t('trip.day', { day: formatCount(dayNo, language) })}:{' '}
                          </span>
                          {day.title}
                        </span>
                      </span>
                    ),
                    aside: (
                      <span
                        className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${difficultyStyle[day.difficulty]}`}
                      >
                        {t(`difficulty.${day.difficulty}`)}
                      </span>
                    ),
                    content: <p className="pl-11 leading-relaxed">{day.details}</p>,
                  };
                })}
              />
            </DetailSection>

            <CostBreakdown trip={trip} language={language} />

            <div className="grid gap-5 md:grid-cols-2">
              <InfoCard icon={ShieldCheck} title={t('trip.safetyTitle')} tone="hill">
                {(['verified', 'sos', 'escrow'] as const).map((key) => (
                  <li key={key}>{t(`trip.safety.${key}`)}</li>
                ))}
              </InfoCard>
              <InfoCard icon={ReceiptText} title={t('trip.refunds.title')} tone="turmeric">
                {(['early', 'middle', 'late', 'host', 'closure'] as const).map((key) => (
                  <li key={key}>{t(`trip.refunds.${key}`)}</li>
                ))}
              </InfoCard>
            </div>
          </div>

          <BookingCard trip={trip} language={language} />
        </div>
      </div>

      {/* Phones: the booking card is far down the page, so the price and a way to it stay in reach. */}
      <div className="fixed inset-x-0 bottom-0 z-30 border-t border-hill/10 bg-white/95 px-4 py-3 shadow-[0_-8px_24px_rgba(15,42,31,0.08)] backdrop-blur lg:hidden">
        <div className="flex items-center justify-between gap-4">
          <p>
            <span className="block font-display text-xl font-bold leading-none text-deep">
              {formatMoney(trip.pricePerPerson, language)}
            </span>
            <span className="text-xs text-deep/60">
              {t('trips.perPerson')} ·{' '}
              {seatsLeft > 0
                ? t('trip.seatsOf', {
                    left: formatCount(seatsLeft, language),
                    total: formatCount(seats, language),
                  })
                : t('trips.full')}
            </span>
          </p>
          <a
            href="#booking"
            className="rounded-full bg-turmeric px-5 py-3 text-sm font-semibold text-night shadow-sm transition hover:bg-dusk"
          >
            {t('trip.reserveCta')}
          </a>
        </div>
      </div>
    </article>
  );
}

function DetailSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section>
      <h2 className="flex items-center gap-3 text-2xl font-semibold">
        <span aria-hidden="true" className="h-6 w-1.5 rounded-full bg-turmeric" />
        {title}
      </h2>
      <div className="mt-5">{children}</div>
    </section>
  );
}

function InfoCard({
  icon: Icon,
  title,
  tone,
  children,
}: {
  icon: LucideIcon;
  title: string;
  tone: 'hill' | 'turmeric';
  children: ReactNode;
}) {
  return (
    <section className="rounded-2xl bg-mist p-6">
      <h2 className="flex items-center gap-3 text-lg font-semibold">
        <span
          className={`flex h-10 w-10 items-center justify-center rounded-xl ${
            tone === 'hill' ? 'bg-hill text-white' : 'bg-turmeric text-night'
          }`}
        >
          <Icon aria-hidden="true" className="h-5 w-5" />
        </span>
        {title}
      </h2>
      <ul className="mt-4 space-y-2.5 text-sm leading-relaxed [&>li]:relative [&>li]:pl-5 [&>li]:before:absolute [&>li]:before:left-0 [&>li]:before:top-2 [&>li]:before:h-1.5 [&>li]:before:w-1.5 [&>li]:before:rounded-full [&>li]:before:bg-hill">
        {children}
      </ul>
    </section>
  );
}

function CostBreakdown({ trip, language }: { trip: TripDetail; language: Language }) {
  const { t } = useTranslation();
  const total = asNumber(trip.pricePerPerson);

  return (
    <DetailSection title={t('trip.costs')}>
      <p className="-mt-2 text-sm">{t('trip.costsHint')}</p>

      <div className="mt-5 rounded-2xl bg-white p-6 shadow-sm ring-1 ring-hill/10">
        {/* One bar, split by category: the shape of the price at a glance. */}
        <div className="flex h-3 gap-0.5 overflow-hidden rounded-full" aria-hidden="true">
          {trip.costItems.map((item, index) => (
            <span
              key={index}
              className={costColour[item.category]}
              style={{ width: `${(asNumber(item.amount) / total) * 100}%` }}
            />
          ))}
        </div>

        <ul className="mt-5 divide-y divide-hill/10">
          {trip.costItems.map((item, index) => (
            <li key={index} className="flex items-start justify-between gap-4 py-3">
              <div className="flex items-start gap-3">
                <span
                  className={`mt-1.5 h-3 w-3 shrink-0 rounded-sm ${costColour[item.category]}`}
                  aria-hidden="true"
                />
                <div>
                  <p className="font-semibold text-deep">{t(`cost.${item.category}`)}</p>
                  {item.description && <p className="text-sm text-deep/60">{item.description}</p>}
                </div>
              </div>
              <p className="shrink-0 font-semibold text-deep">
                {formatMoney(item.amount, language)}
              </p>
            </li>
          ))}
        </ul>

        <div className="mt-2 flex items-center justify-between rounded-xl bg-mist px-4 py-3">
          <p className="font-semibold text-deep">{t('trip.total')}</p>
          <p className="font-display text-2xl font-bold text-hill">
            {formatMoney(total, language)}
          </p>
        </div>
      </div>
    </DetailSection>
  );
}

/** The booking card: price on a coloured header, the trip's terms, the seats, and the action. */
function BookingCard({ trip, language }: { trip: TripDetail; language: Language }) {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const userId = useAuthStore((state) => state.user?.id);
  const [joining, setJoining] = useState(false);
  const [reporting, setReporting] = useState(false);
  const seats = asNumber(trip.seats);
  const seatsLeft = asNumber(trip.seatsLeft);
  const taken = seats - seatsLeft;
  const isOwnTrip = userId !== undefined && userId === asNumber(trip.host.id);
  const mix = trip.groupMix;

  return (
    <aside id="booking" className="scroll-mt-24 lg:sticky lg:top-24 lg:self-start">
      <div className="overflow-hidden rounded-2xl bg-mist shadow-xl ring-1 ring-hill/10">
        <div className="bg-hill px-6 py-5 text-white">
          <p className="text-sm text-white/80">{t('trip.from')}</p>
          <p className="font-display text-4xl font-bold">
            {formatMoney(trip.pricePerPerson, language)}
          </p>
          <p className="text-sm text-white/80">{t('trips.perPerson')}</p>
        </div>

        <dl className="space-y-3 px-6 pt-5 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-deep/60">{t('trip.dates')}</dt>
            <dd className="text-right font-semibold text-deep">
              {formatDate(trip.startDate, language)} – {formatDate(trip.endDate, language)}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-deep/60">{t('trip.group')}</dt>
            <dd className="font-semibold text-deep">{t(`groupType.${trip.groupType}`)}</dd>
          </div>
          <div className="rounded-xl bg-white p-4 ring-1 ring-hill/10">
            <div className="flex justify-between gap-4">
              <dt className="text-deep/60">{t('trip.seats')}</dt>
              <dd className="font-semibold text-deep">
                {seatsLeft > 0
                  ? t('trip.seatsOf', {
                      left: formatCount(seatsLeft, language),
                      total: formatCount(seats, language),
                    })
                  : t('trips.full')}
              </dd>
            </div>
            <div className="mt-3 flex flex-wrap gap-1" aria-hidden="true">
              {Array.from({ length: seats }, (_, index) => (
                <span
                  key={index}
                  className={`h-3.5 w-3.5 rounded ${index < taken ? 'bg-hill' : 'bg-hill/15'}`}
                />
              ))}
            </div>
            {mix && taken > 0 && (
              <p className="mt-3 text-xs text-deep/70">
                {t('trip.groupMix', {
                  women: formatCount(mix.women, language),
                  men: formatCount(mix.men, language),
                  others: formatCount(mix.others, language),
                })}
              </p>
            )}
          </div>
        </dl>

        <div className="px-6 pb-6 pt-5">
          {isOwnTrip ? (
            <Link
              to={`/host/trips/${asNumber(trip.id)}/requests`}
              className="block w-full rounded-full bg-hill px-4 py-3.5 text-center font-semibold text-white transition hover:bg-deep"
            >
              {t('hostTrips.requests')}
            </Link>
          ) : status === 'authenticated' ? (
            <button
              type="button"
              disabled={seatsLeft <= 0}
              onClick={() => setJoining(true)}
              className="w-full rounded-full bg-turmeric px-4 py-3.5 font-semibold text-night shadow-sm transition hover:bg-dusk disabled:cursor-not-allowed disabled:opacity-50"
            >
              {seatsLeft > 0 ? t('trip.request') : t('trips.full')}
            </button>
          ) : (
            <Link
              to="/login"
              className="block w-full rounded-full bg-turmeric px-4 py-3.5 text-center font-semibold text-night shadow-sm transition hover:bg-dusk"
            >
              {t('trip.signInToJoin')}
            </Link>
          )}
          <JoinRequestDialog
            tripId={asNumber(trip.id)}
            open={joining}
            onClose={() => setJoining(false)}
          />
          <p className="mt-4 flex gap-2 text-xs leading-relaxed text-deep/70">
            <LockKeyhole aria-hidden="true" className="h-4 w-4 shrink-0 text-hill" />
            {t('trip.escrowNote')}
          </p>
        </div>
      </div>

      <div className="mt-5 flex items-center gap-4 rounded-2xl bg-white p-5 shadow-sm ring-1 ring-hill/10">
        <Avatar
          userId={asNumber(trip.host.id)}
          name={trip.host.displayName}
          size="md"
          className="h-14! w-14! ring-4 ring-mist"
        />
        <div className="min-w-0">
          <p className="text-xs text-deep/60">{t('trip.host')}</p>
          <p className="font-display font-semibold text-deep">
            <Link to={`/users/${asNumber(trip.host.id)}`} className="hover:text-hill">
              {trip.host.displayName ?? t('trips.aHost')}
            </Link>
          </p>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <VerificationBadge level={trip.host.verifiedLevel} />
            <span className="text-xs text-deep/60">
              {t('trip.memberSince', { date: formatMonthYear(trip.host.memberSince, language) })}
            </span>
          </div>
        </div>
      </div>

      {status === 'authenticated' && !isOwnTrip && (
        <>
          <button
            type="button"
            onClick={() => setReporting(true)}
            className="mt-4 inline-flex items-center gap-1.5 text-sm text-deep/60 transition hover:text-jamdani"
          >
            <Flag aria-hidden="true" className="h-4 w-4" />
            {t('report.open.Trip')}
          </button>
          <ReportDialog
            kind="Trip"
            targetId={asNumber(trip.id)}
            open={reporting}
            onClose={() => setReporting(false)}
          />
        </>
      )}

      <Link
        to="/trips"
        className="mt-4 flex items-center gap-1.5 text-sm font-medium text-hill hover:underline lg:hidden"
      >
        <ArrowLeft aria-hidden="true" className="h-4 w-4" />
        {t('trip.back')}
      </Link>
    </aside>
  );
}

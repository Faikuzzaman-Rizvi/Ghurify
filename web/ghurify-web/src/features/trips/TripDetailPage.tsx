import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ApiError, asNumber } from '@/api/client';
import { Scenery } from '@/components/Scenery';
import { EmptyState, ErrorState } from '@/components/States';
import { useAuthStore } from '@/features/auth/authStore';
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
      <div className="mx-auto max-w-3xl px-4 pt-16">
        <EmptyState
          title={t('trip.notFound')}
          action={
            <Link
              to="/trips"
              className="mt-2 rounded-full bg-hill px-4 py-2 text-sm font-medium text-white"
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
      <div role="status" className="mx-auto max-w-6xl px-4 pt-8">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-72 animate-pulse rounded-4xl bg-hill/10" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-3xl px-4 pt-16">
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
  const cautionNote =
    language === 'bn' ? trip.destination.statusNoteBn : trip.destination.statusNote;

  return (
    <article>
      <header className="relative isolate overflow-hidden">
        <Scenery kind={trip.destination.kind} className="absolute inset-0 -z-10 h-full w-full" />
        <div className="absolute inset-0 -z-10 bg-linear-to-t from-deep/85 via-deep/30 to-transparent" />

        <div className="mx-auto max-w-6xl px-4 pb-10 pt-24 text-white sm:pt-36">
          <Link to="/trips" className="text-sm font-medium text-white/85 hover:text-white">
            ← {t('trip.back')}
          </Link>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Link
              to={`/destinations/${trip.destination.slug}`}
              className="rounded-full bg-white/90 px-3 py-1 text-xs font-semibold text-deep"
            >
              📍 {place}
            </Link>
            <GroupBadge groupType={trip.groupType} />
            <StatusBadge status={trip.destination.status} />
          </div>
          <h1 className="mt-3 max-w-3xl text-3xl font-extrabold sm:text-5xl">{trip.title}</h1>
          <p className="mt-2 text-white/85">
            {formatDateRange(trip.startDate, trip.endDate, language)} ·{' '}
            {t('trips.days', { count: days, n: formatCount(days, language) })}
          </p>
        </div>
      </header>

      <div className="mx-auto grid max-w-6xl gap-8 px-4 pt-8 lg:grid-cols-[1fr_360px]">
        <div className="flex flex-col gap-8">
          {trip.destination.status !== 'Open' && cautionNote && (
            <aside
              role="note"
              className="flex gap-3 rounded-2xl bg-amber-50 p-4 ring-1 ring-amber-600/30"
            >
              <span aria-hidden="true" className="text-xl">
                ⚠️
              </span>
              <div>
                <p className="font-semibold text-amber-900">{t('trip.caution', { place })}</p>
                <p className="text-sm text-amber-900/80">{cautionNote}</p>
              </div>
            </aside>
          )}

          <section>
            <h2 className="text-2xl font-bold text-deep">{t('trip.overview')}</h2>
            <p className="mt-2 text-deep/80">{trip.summary}</p>
          </section>

          <Itinerary trip={trip} language={language} />

          <CostBreakdown trip={trip} language={language} />

          <section className="rounded-3xl bg-hill/5 p-6 ring-1 ring-hill/10">
            <h2 className="text-xl font-bold text-deep">{t('trip.safetyTitle')}</h2>
            <ul className="mt-3 space-y-2 text-sm text-deep/80">
              {(['verified', 'sos', 'escrow'] as const).map((key) => (
                <li key={key} className="flex items-start gap-2">
                  <span aria-hidden="true" className="text-hill">
                    ✓
                  </span>
                  {t(`trip.safety.${key}`)}
                </li>
              ))}
            </ul>
          </section>
        </div>

        <BookingCard trip={trip} language={language} />
      </div>
    </article>
  );
}

function Itinerary({ trip, language }: { trip: TripDetail; language: Language }) {
  const { t } = useTranslation();

  return (
    <section>
      <h2 className="text-2xl font-bold text-deep">{t('trip.itinerary')}</h2>
      <ol className="mt-4 border-l-2 border-dashed border-hill/30 pl-6">
        {trip.itinerary.map((day) => {
          const dayNo = asNumber(day.dayNo);
          return (
            <li key={dayNo} className="relative pb-6 last:pb-0">
              <span className="absolute -left-[2.1rem] flex h-7 w-7 items-center justify-center rounded-full bg-turmeric font-display text-sm font-bold text-deep ring-4 ring-sand">
                {formatCount(dayNo, language)}
              </span>
              <div className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-hill/10">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 className="font-bold text-deep">
                    <span className="text-turmeric">
                      {t('trip.day', { day: formatCount(dayNo, language) })}
                    </span>
                    {' · '}
                    {day.title}
                  </h3>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${difficultyStyle[day.difficulty]}`}
                  >
                    {t(`difficulty.${day.difficulty}`)}
                  </span>
                </div>
                <p className="mt-1 text-sm text-deep/75">{day.details}</p>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function CostBreakdown({ trip, language }: { trip: TripDetail; language: Language }) {
  const { t } = useTranslation();
  const total = asNumber(trip.pricePerPerson);

  return (
    <section>
      <h2 className="text-2xl font-bold text-deep">{t('trip.costs')}</h2>
      <p className="mt-1 text-sm text-deep/70">{t('trip.costsHint')}</p>

      <div className="mt-4 rounded-3xl bg-white p-5 shadow-sm ring-1 ring-hill/10">
        {/* One bar, split by category: the shape of the price at a glance. */}
        <div className="flex h-3 overflow-hidden rounded-full" aria-hidden="true">
          {trip.costItems.map((item, index) => (
            <span
              key={index}
              className={costColour[item.category]}
              style={{ width: `${(asNumber(item.amount) / total) * 100}%` }}
            />
          ))}
        </div>

        <ul className="mt-4 divide-y divide-hill/10">
          {trip.costItems.map((item, index) => (
            <li key={index} className="flex items-start justify-between gap-4 py-2.5">
              <div className="flex items-start gap-3">
                <span
                  className={`mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full ${costColour[item.category]}`}
                  aria-hidden="true"
                />
                <div>
                  <p className="font-medium text-deep">{t(`cost.${item.category}`)}</p>
                  {item.description && <p className="text-sm text-deep/60">{item.description}</p>}
                </div>
              </div>
              <p className="shrink-0 font-semibold text-deep">
                {formatMoney(item.amount, language)}
              </p>
            </li>
          ))}
        </ul>

        <div className="mt-2 flex items-center justify-between border-t-2 border-hill/15 pt-3">
          <p className="font-bold text-deep">{t('trip.total')}</p>
          <p className="font-display text-2xl font-extrabold text-hill">
            {formatMoney(total, language)}
          </p>
        </div>
      </div>
    </section>
  );
}

/** The price card, styled as a ticket: the action a traveller came for. */
function BookingCard({ trip, language }: { trip: TripDetail; language: Language }) {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const [showNotice, setShowNotice] = useState(false);
  const seats = asNumber(trip.seats);
  const seatsLeft = asNumber(trip.seatsLeft);
  const taken = seats - seatsLeft;

  return (
    <aside className="lg:sticky lg:top-24 lg:self-start">
      <div className="overflow-hidden rounded-3xl bg-white shadow-xl ring-1 ring-hill/10">
        <div className="bg-hill px-6 py-5 text-white">
          <p className="font-display text-4xl font-extrabold">
            {formatMoney(trip.pricePerPerson, language)}
          </p>
          <p className="text-sm text-white/80">{t('trips.perPerson')}</p>
        </div>

        {/* Ticket perforation. */}
        <div className="relative h-4" aria-hidden="true">
          <span className="absolute -left-2 top-0 h-4 w-4 rounded-full bg-sand" />
          <span className="absolute -right-2 top-0 h-4 w-4 rounded-full bg-sand" />
          <span className="absolute inset-x-4 top-1/2 border-t-2 border-dashed border-hill/20" />
        </div>

        <dl className="space-y-3 px-6 pb-2 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-deep/60">{t('trip.dates')}</dt>
            <dd className="text-right font-medium text-deep">
              {formatDate(trip.startDate, language)} – {formatDate(trip.endDate, language)}
            </dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-deep/60">{t('trip.group')}</dt>
            <dd className="font-medium text-deep">{t(`groupType.${trip.groupType}`)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-deep/60">{t('trip.meetingPoint')}</dt>
            <dd className="text-right font-medium text-deep">{trip.meetingPoint}</dd>
          </div>
          <div>
            <div className="flex justify-between gap-4">
              <dt className="text-deep/60">{t('trip.seats')}</dt>
              <dd className="font-medium text-deep">
                {seatsLeft > 0
                  ? t('trip.seatsOf', {
                      left: formatCount(seatsLeft, language),
                      total: formatCount(seats, language),
                    })
                  : t('trips.full')}
              </dd>
            </div>
            <div className="mt-2 flex flex-wrap gap-1" aria-hidden="true">
              {Array.from({ length: seats }, (_, index) => (
                <span
                  key={index}
                  className={`h-3 w-3 rounded-sm ${index < taken ? 'bg-hill' : 'bg-hill/15'}`}
                />
              ))}
            </div>
          </div>
        </dl>

        <div className="px-6 pb-6 pt-4">
          {status === 'authenticated' ? (
            <button
              type="button"
              disabled={seatsLeft <= 0}
              onClick={() => setShowNotice(true)}
              className="w-full rounded-2xl bg-turmeric px-4 py-3 font-display text-lg font-bold text-deep transition hover:bg-dusk disabled:opacity-50"
            >
              {seatsLeft > 0 ? t('trip.request') : t('trips.full')}
            </button>
          ) : (
            <Link
              to="/login"
              className="block w-full rounded-2xl bg-turmeric px-4 py-3 text-center font-display text-lg font-bold text-deep transition hover:bg-dusk"
            >
              {t('trip.signInToJoin')}
            </Link>
          )}
          {showNotice && (
            <p role="status" className="mt-3 rounded-xl bg-mist p-3 text-sm text-deep/80">
              {t('trip.joinSoon')}
            </p>
          )}
          <p className="mt-3 flex gap-2 text-xs text-deep/60">
            <span aria-hidden="true">🔐</span>
            {t('trip.escrowNote')}
          </p>
        </div>
      </div>

      <div className="mt-4 flex items-center gap-3 rounded-3xl bg-white p-5 shadow-sm ring-1 ring-hill/10">
        <span
          className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-hill font-display text-xl font-bold text-white"
          aria-hidden="true"
        >
          {(trip.host.displayName ?? 'G').charAt(0)}
        </span>
        <div>
          <p className="text-xs text-deep/60">{t('trip.host')}</p>
          <p className="font-bold text-deep">{trip.host.displayName ?? t('trips.aHost')}</p>
          <p className="text-xs text-deep/60">
            {t('trip.memberSince', { date: formatMonthYear(trip.host.memberSince, language) })}
          </p>
        </div>
      </div>
    </aside>
  );
}

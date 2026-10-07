import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  BellRing,
  CalendarDays,
  ClipboardList,
  MapPin,
  MessageCircle,
  Pencil,
  Plus,
  Rocket,
  ShieldAlert,
  Tent,
  Users,
  Wallet,
  XCircle,
  type LucideIcon,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  accentButtonClass,
  cardClass,
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
} from '@/components/Field';
import { CardSkeletons, EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateRange, formatMoney, toLanguage } from '@/lib/format';
import type { HostTripSummary, TripStatus } from './hostApi';
import { useCancelTrip, useMyHostedTrips, usePublishTrip } from './useHostTrips';

const statusStyle: Record<NonNullable<TripStatus>, string> = {
  Draft: 'bg-white text-deep',
  Published: 'bg-emerald-500 text-white',
  Full: 'bg-turmeric text-night',
  Cancelled: 'bg-jamdani text-white',
  Completed: 'bg-river text-white',
};

/** The tabs: what a host is usually looking for, rather than every status separately. */
const tabs = {
  all: () => true,
  live: (status: NonNullable<TripStatus>) => status === 'Published' || status === 'Full',
  drafts: (status: NonNullable<TripStatus>) => status === 'Draft',
  past: (status: NonNullable<TripStatus>) => status === 'Completed' || status === 'Cancelled',
} as const;
type Tab = keyof typeof tabs;

/** Every trip the signed-in host runs, with what needs their attention. */
export function HostTripsPage() {
  const { t } = useTranslation();
  const trips = useMyHostedTrips();
  const [tab, setTab] = useState<Tab>('all');

  const all = trips.data ?? [];
  const shown = all.filter((trip) => tabs[tab](trip.status ?? 'Draft'));

  return (
    <>
      <PageBanner
        compact
        slug="rangamati"
        kind="Lake"
        eyebrow={t('hosting.title')}
        titleKey="hostTrips.titleAccent"
        aside={
          <div className="flex flex-wrap gap-3">
            <Link
              to="/host/payouts"
              className="inline-flex items-center gap-2 rounded-full border border-white/40 px-5 py-3 text-sm font-semibold text-white backdrop-blur transition hover:bg-white/15"
            >
              <Wallet aria-hidden="true" className="h-4 w-4" />
              {t('payouts.title')}
            </Link>
            <Link to="/host/trips/new" className={accentButtonClass}>
              <Plus aria-hidden="true" className="h-4 w-4" />
              {t('hostTrips.new')}
            </Link>
          </div>
        }
      >
        <p className="mt-3 max-w-xl text-white/80">{t('hostTrips.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 flex flex-col gap-8 pb-8">
        <Stats trips={all} loading={trips.isPending} />

        {trips.isPending && (
          <div className="grid gap-6 lg:grid-cols-2">
            <CardSkeletons count={2} />
          </div>
        )}

        {trips.isError && (
          <ErrorState message={errorText(trips.error, t)} onRetry={() => void trips.refetch()} />
        )}

        {trips.data?.length === 0 && (
          <EmptyState
            title={t('hostTrips.empty')}
            hint={t('hostTrips.emptyHint')}
            action={
              <Link to="/host/trips/new" className={`${primaryButtonClass} mt-2`}>
                <Plus aria-hidden="true" className="h-4 w-4" />
                {t('hostTrips.new')}
              </Link>
            }
          />
        )}

        {all.length > 0 && (
          <section aria-labelledby="hosted-heading">
            <h2 id="hosted-heading" className="sr-only">
              {t('hostTrips.title')}
            </h2>
            <div
              role="group"
              aria-label={t('hostTrips.filter')}
              className="flex gap-1 overflow-x-auto rounded-full bg-mist p-1 sm:w-fit"
            >
              {(Object.keys(tabs) as Tab[]).map((key) => {
                const count = all.filter((trip) => tabs[key](trip.status ?? 'Draft')).length;
                return (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={tab === key}
                    onClick={() => setTab(key)}
                    className={`flex shrink-0 items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold transition ${
                      tab === key ? 'bg-white text-hill shadow-sm' : 'text-deep/70 hover:text-deep'
                    }`}
                  >
                    {t(`hostTrips.tabs.${key}`)}
                    <span
                      className={`rounded-full px-2 py-0.5 text-xs ${
                        tab === key ? 'bg-hill text-white' : 'bg-white text-deep/60'
                      }`}
                    >
                      {count}
                    </span>
                  </button>
                );
              })}
            </div>

            {shown.length === 0 ? (
              <p className="mt-6 rounded-2xl border-2 border-dashed border-hill/15 px-6 py-10 text-center text-deep/60">
                {t('hostTrips.noneInTab')}
              </p>
            ) : (
              <ul className="mt-6 grid gap-6 lg:grid-cols-2">
                {shown.map((trip) => (
                  <li key={String(trip.id)}>
                    <HostTripCard trip={trip} />
                  </li>
                ))}
              </ul>
            )}
          </section>
        )}
      </div>
    </>
  );
}

/** Four figures across the top, all counted from the host's own trips. */
function Stats({ trips, loading }: { trips: HostTripSummary[]; loading: boolean }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  const live = trips.filter((trip) => trip.status === 'Published' || trip.status === 'Full');
  const pending = trips.reduce((sum, trip) => sum + asNumber(trip.pendingRequests), 0);
  const seats = live.reduce((sum, trip) => sum + asNumber(trip.seats), 0);
  const taken = live.reduce((sum, trip) => sum + asNumber(trip.seatsTaken), 0);

  const figures: { key: string; icon: LucideIcon; value: string; highlight?: boolean }[] = [
    { key: 'total', icon: Tent, value: formatCount(trips.length, language) },
    { key: 'live', icon: Rocket, value: formatCount(live.length, language) },
    {
      key: 'requests',
      icon: BellRing,
      value: formatCount(pending, language),
      highlight: pending > 0,
    },
    {
      key: 'seats',
      icon: Users,
      value: `${formatCount(taken, language)}/${formatCount(seats, language)}`,
    },
  ];

  return (
    <ul className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {figures.map(({ key, icon: Icon, value, highlight }) => (
        <li
          key={key}
          className={`${cardClass} flex items-center gap-4 p-5! ${highlight ? 'ring-2! ring-turmeric!' : ''}`}
        >
          <span
            className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${
              highlight ? 'bg-turmeric text-night' : 'bg-hill/10 text-hill'
            }`}
          >
            <Icon aria-hidden="true" className="h-6 w-6" />
          </span>
          <span className="min-w-0">
            <span className="block font-display text-2xl font-bold text-deep">
              {loading ? '—' : value}
            </span>
            <span className="block text-xs leading-tight text-deep/60 sm:text-sm">
              {t(`hostTrips.stats.${key}`)}
            </span>
          </span>
        </li>
      ))}
    </ul>
  );
}

function HostTripCard({ trip }: { trip: HostTripSummary }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const publish = usePublishTrip();
  const id = asNumber(trip.id);
  const cancelTrip = useCancelTrip(id);
  const pending = asNumber(trip.pendingRequests);
  const seats = asNumber(trip.seats);
  const taken = asNumber(trip.seatsTaken);
  const status = trip.status ?? 'Draft';
  const editable = status === 'Draft' || status === 'Published' || status === 'Full';
  const active = status === 'Published' || status === 'Full';
  const place = language === 'bn' ? trip.destination.nameBn : trip.destination.name;

  return (
    <article className={`${cardClass} flex h-full flex-col overflow-hidden p-0! sm:flex-row`}>
      <div className="relative h-44 shrink-0 sm:h-auto sm:w-48">
        <Photo
          slug={trip.destination.slug}
          kind={trip.destination.kind}
          cut="card"
          decorative
          className="absolute! inset-0"
        />
        <span
          className={`absolute left-3 top-3 rounded-full px-3 py-1 text-xs font-semibold shadow-sm ${statusStyle[status]}`}
        >
          {t(`tripStatus.${status}`)}
        </span>
      </div>

      <div className="flex flex-1 flex-col gap-4 p-5 sm:p-6">
        <div>
          <h3 className="text-lg font-semibold leading-snug">
            {active ? (
              <Link to={`/trips/${id}`} className="hover:text-hill">
                {trip.title}
              </Link>
            ) : (
              trip.title
            )}
          </h3>
          <p className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-deep/70">
            <span className="inline-flex items-center gap-1.5">
              <MapPin aria-hidden="true" className="h-4 w-4 text-ochre" />
              {place}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <CalendarDays aria-hidden="true" className="h-4 w-4 text-ochre" />
              {formatDateRange(trip.startDate, trip.endDate, language)}
            </span>
          </p>
        </div>

        <div>
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="text-deep/70">
              {t('hostTrips.seats', {
                taken: formatCount(taken, language),
                total: formatCount(seats, language),
              })}
            </span>
            <span className="font-display text-lg font-bold text-hill">
              {formatMoney(trip.pricePerPerson, language)}
            </span>
          </div>
          <div aria-hidden="true" className="mt-2 h-2 overflow-hidden rounded-full bg-mist">
            <div
              className="h-full rounded-full bg-hill"
              style={{ width: `${seats > 0 ? Math.min(100, (taken / seats) * 100) : 0}%` }}
            />
          </div>
        </div>

        {pending > 0 && (
          <Link
            to={`/host/trips/${id}/requests`}
            className="flex items-center gap-2 rounded-xl bg-turmeric/15 px-4 py-3 text-sm font-semibold text-deep ring-1 ring-turmeric/40 transition hover:bg-turmeric/25"
          >
            <BellRing aria-hidden="true" className="h-4 w-4 text-ochre" />
            {t('hostTrips.pending', { count: pending, n: formatCount(pending, language) })}
          </Link>
        )}

        <div className="mt-auto flex flex-wrap gap-2 border-t border-hill/10 pt-4 *:px-4! *:py-2! *:text-sm">
          {status === 'Draft' && (
            <button
              type="button"
              className={primaryButtonClass}
              disabled={publish.isPending}
              onClick={() => publish.mutate(id)}
            >
              <Rocket aria-hidden="true" className="h-4 w-4" />
              {t('wizard.publish')}
            </button>
          )}
          {editable && (
            <Link to={`/host/trips/${id}/edit`} className={secondaryButtonClass}>
              <Pencil aria-hidden="true" className="h-4 w-4" />
              {t('hostTrips.edit')}
            </Link>
          )}
          {status !== 'Draft' && (
            <Link to={`/host/trips/${id}/requests`} className={secondaryButtonClass}>
              <ClipboardList aria-hidden="true" className="h-4 w-4" />
              {t('hostTrips.requests')}
            </Link>
          )}
          {status !== 'Draft' && status !== 'Cancelled' && (
            <Link to={`/trips/${id}/chat`} className={secondaryButtonClass}>
              <MessageCircle aria-hidden="true" className="h-4 w-4" />
              {t('chat.open')}
            </Link>
          )}
          {active && (
            <Link to={`/trips/${id}/safety`} className={secondaryButtonClass}>
              <ShieldAlert aria-hidden="true" className="h-4 w-4" />
              {t('safety.open')}
            </Link>
          )}
          {(active || status === 'Draft') && (
            <button
              type="button"
              className={dangerButtonClass}
              disabled={cancelTrip.isPending}
              onClick={() => {
                if (window.confirm(t('hostTrips.cancelConfirm'))) cancelTrip.mutate();
              }}
            >
              <XCircle aria-hidden="true" className="h-4 w-4" />
              {t('hostTrips.cancel')}
            </button>
          )}
        </div>
        {cancelTrip.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(cancelTrip.error, t)}
          </p>
        )}
        {publish.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(publish.error, t)}
          </p>
        )}
      </div>
    </article>
  );
}

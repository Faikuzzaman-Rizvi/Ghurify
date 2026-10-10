import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowUpRight, CalendarDays, UserRound, XCircle } from 'lucide-react';

import { asNumber } from '@/api/client';
import { inputClass } from '@/components/Field';
import { Photo } from '@/components/ui/Photo';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import type { DestinationSummary } from '@/features/trips/tripsApi';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { formatDateRange, formatMoney, toLanguage, type Language } from '@/lib/format';
import { adminApi, type AdminTripItem, type TripStatus } from './adminApi';
import {
  AdminPageHeader,
  AdminPager,
  AdminSearchBar,
  CardGridSkeleton,
  StatusPill,
} from './AdminUi';
import {
  adminCardClass,
  smallDangerButtonClass,
  smallSecondaryButtonClass,
  type Tone,
} from './adminStyles';
import { ReasonDialog } from './ReasonDialog';

const statuses: readonly (TripStatus | '')[] = [
  '',
  'Draft',
  'Published',
  'Full',
  'Cancelled',
  'Completed',
];

const statusTone: Record<TripStatus, Tone> = {
  Draft: 'neutral',
  Published: 'good',
  Full: 'info',
  Cancelled: 'bad',
  Completed: 'neutral',
};

/** Every trip in any status, and the power to cancel one with full refunds. */
export function AdminTripsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<TripStatus | ''>('');
  const [page, setPage] = useState(1);
  const [cancelling, setCancelling] = useState<AdminTripItem | null>(null);
  // Only for each trip's photo: the trip row names its destination, the list knows its picture.
  const destinations = useDestinations();

  const trips = useQuery({
    queryKey: ['admin', 'trips', search, status, page],
    queryFn: ({ signal }) => adminApi.trips(search, status, page, signal),
    placeholderData: (previous) => previous,
  });

  const cancel = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => adminApi.cancelTrip(id, reason),
    onSuccess: () => {
      setCancelling(null);
      void queryClient.invalidateQueries({ queryKey: ['admin', 'trips'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    },
  });

  const total = trips.data ? asNumber(trips.data.totalCount) : 0;
  const pageSize = trips.data ? asNumber(trips.data.pageSize) : 25;
  const placeOf = (trip: AdminTripItem) =>
    destinations.data?.find(
      (destination) =>
        destination.name === trip.destinationName || destination.nameBn === trip.destinationName,
    );

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.operations')}
        title={t('admin.trips.title')}
        description={t('admin.trips.lead')}
      />

      <AdminSearchBar
        id="trip-search"
        label={t('admin.trips.search')}
        placeholder={t('admin.trips.searchHint')}
        value={draft}
        onChange={setDraft}
        onSubmit={() => {
          setSearch(draft.trim());
          setPage(1);
        }}
        submitLabel={t('admin.trips.search')}
      >
        <label htmlFor="trip-status" className="sr-only">
          {t('admin.trips.status')}
        </label>
        <Select
          id="trip-status"
          className="sm:w-48"
          value={status}
          onChange={(value) => {
            setStatus(value as TripStatus | '');
            setPage(1);
          }}
          options={statuses.map((option) => ({
            value: option,
            label: option ? t(`tripStatus.${option}`) : t('admin.trips.allStatuses'),
          }))}
          buttonClassName={`${inputClass} cursor-pointer`}
        />
      </AdminSearchBar>

      {trips.isPending && <CardGridSkeleton className="h-96" />}
      {trips.isError && (
        <ErrorState message={errorText(trips.error, t)} onRetry={() => void trips.refetch()} />
      )}
      {trips.data?.items.length === 0 && <EmptyState title={t('admin.trips.empty')} />}

      {trips.data && trips.data.items.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {trips.data.items.map((trip) => (
            <li key={String(trip.id)} className="flex">
              <TripCard
                trip={trip}
                place={placeOf(trip)}
                language={language}
                onCancel={() => setCancelling(trip)}
              />
            </li>
          ))}
        </ul>
      )}

      <AdminPager page={page} pageSize={pageSize} total={total} onPage={setPage} />

      <ReasonDialog
        key={cancelling ? String(cancelling.id) : 'none'}
        open={cancelling !== null}
        title={t('admin.trips.confirmTitle', { title: cancelling?.title ?? '' })}
        description={t('admin.trips.confirmText')}
        confirmLabel={t('admin.trips.cancel')}
        danger
        pending={cancel.isPending}
        error={cancel.error}
        onClose={() => {
          cancel.reset();
          setCancelling(null);
        }}
        onConfirm={(reason) => cancelling && cancel.mutate({ id: asNumber(cancelling.id), reason })}
      />
    </div>
  );
}

/** One trip: its place's photo, when and who, how full it is, and what it costs. */
function TripCard({
  trip,
  place,
  language,
  onCancel,
}: {
  trip: AdminTripItem;
  place: DestinationSummary | undefined;
  language: Language;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const id = asNumber(trip.id);
  const seats = asNumber(trip.seats);
  const taken = asNumber(trip.seatsTaken);
  const filled = seats > 0 ? Math.min(100, Math.round((taken / seats) * 100)) : 0;
  const live = trip.status === 'Published' || trip.status === 'Full' || trip.status === 'Draft';

  return (
    <article className={adminCardClass}>
      <div className="relative h-36 shrink-0">
        <Photo
          slug={place?.slug ?? ''}
          kind={place?.kind ?? 'Hills'}
          cut="card"
          sizes="(min-width: 1280px) 22rem, (min-width: 640px) 45vw, 100vw"
          decorative
          className="absolute! inset-0"
          imgClassName="transition-transform duration-700 group-hover/card:scale-105 motion-reduce:transition-none"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-linear-to-t from-night/70 via-night/10 to-transparent"
        />
        <StatusPill tone={statusTone[trip.status]} onPhoto className="absolute left-4 top-4">
          {t(`tripStatus.${trip.status}`)}
        </StatusPill>
        <span className="absolute right-4 top-4 rounded-full bg-night/45 px-2.5 py-1 font-mono text-xs font-semibold text-white ring-1 ring-white/20 backdrop-blur-sm">
          #{id}
        </span>
        <p className="absolute inset-x-4 bottom-3 truncate text-sm font-semibold text-white">
          {trip.destinationName}
        </p>
      </div>

      <div className="flex flex-1 flex-col gap-3.5 p-5">
        <h3 className="font-display text-lg font-semibold leading-snug">
          <Link
            to={`/trips/${id}`}
            className="text-deep underline-offset-4 transition hover:text-hill hover:underline"
          >
            {trip.title}
          </Link>
        </h3>

        <div className="flex flex-col gap-1.5 text-sm text-deep/70">
          <p className="flex items-center gap-2">
            <CalendarDays aria-hidden="true" className="h-4 w-4 text-hill/70" />
            {formatDateRange(trip.startDate, trip.endDate, language)}
          </p>
          <p className="flex items-center gap-2">
            <UserRound aria-hidden="true" className="h-4 w-4 text-hill/70" />
            <Link
              to={`/admin/users/${asNumber(trip.hostId)}`}
              className="font-medium text-hill underline-offset-4 hover:underline"
            >
              {trip.hostName ?? t('admin.noName')}
            </Link>
          </p>
        </div>

        <div className="flex items-end justify-between gap-3">
          <div className="min-w-0 flex-1">
            <p className="mb-1.5 text-xs text-deep/60">
              {t('admin.trips.seats', { taken, seats })}
            </p>
            <div role="presentation" className="h-2 overflow-hidden rounded-full bg-hill/10">
              <div
                className={`h-full rounded-full ${filled >= 100 ? 'bg-turmeric' : 'bg-hill'}`}
                style={{ width: `${filled}%` }}
              />
            </div>
          </div>
          <p className="shrink-0 font-display text-lg font-semibold text-deep">
            {formatMoney(trip.pricePerPerson, language)}
          </p>
        </div>

        <div className="mt-auto flex flex-wrap gap-2 border-t border-hill/8 pt-4">
          <Link to={`/trips/${id}`} className={smallSecondaryButtonClass}>
            <ArrowUpRight aria-hidden="true" className="h-4 w-4" />
            {t('admin.trips.view')}
          </Link>
          {live && (
            <button type="button" className={smallDangerButtonClass} onClick={onCancel}>
              <XCircle aria-hidden="true" className="h-4 w-4" />
              {t('admin.trips.cancel')}
            </button>
          )}
        </div>
      </div>
    </article>
  );
}

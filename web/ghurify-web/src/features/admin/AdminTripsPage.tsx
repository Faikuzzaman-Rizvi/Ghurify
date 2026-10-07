import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Search } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, dangerButtonClass, inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatDateRange, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type AdminTripItem, type TripStatus } from './adminApi';
import { ReasonDialog } from './ReasonDialog';

const statuses: readonly (TripStatus | '')[] = [
  '',
  'Draft',
  'Published',
  'Full',
  'Cancelled',
  'Completed',
];

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

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
        {t('admin.trips.title')}
      </h2>

      <form
        role="search"
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setSearch(draft.trim());
          setPage(1);
        }}
      >
        <label htmlFor="trip-search" className="sr-only">
          {t('admin.trips.search')}
        </label>
        <input
          id="trip-search"
          type="search"
          value={draft}
          placeholder={t('admin.trips.searchHint')}
          onChange={(event) => setDraft(event.target.value)}
          className={`${inputClass} flex-1`}
        />
        <label htmlFor="trip-status" className="sr-only">
          {t('admin.trips.status')}
        </label>
        <Select
          id="trip-status"
          className="sm:w-44"
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
        <button
          type="submit"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep"
        >
          <Search aria-hidden="true" className="h-4 w-4" />
          {t('admin.trips.search')}
        </button>
      </form>

      {trips.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {trips.isError && (
        <ErrorState message={errorText(trips.error, t)} onRetry={() => void trips.refetch()} />
      )}
      {trips.data?.items.length === 0 && <EmptyState title={t('admin.trips.empty')} />}

      {trips.data && trips.data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {trips.data.items.map((trip) => {
            const live =
              trip.status === 'Published' || trip.status === 'Full' || trip.status === 'Draft';
            return (
              <li
                key={String(trip.id)}
                className={`${cardClass} flex flex-wrap items-center justify-between gap-3 p-4!`}
              >
                <div className="min-w-0">
                  <Link
                    to={`/trips/${asNumber(trip.id)}`}
                    className="font-semibold text-deep hover:underline"
                  >
                    #{asNumber(trip.id)} · {trip.title}
                  </Link>
                  <p className="text-sm text-deep/70">
                    {trip.destinationName} ·{' '}
                    {formatDateRange(trip.startDate, trip.endDate, language)} ·{' '}
                    <Link
                      to={`/admin/users/${asNumber(trip.hostId)}`}
                      className="text-hill underline"
                    >
                      {trip.hostName ?? t('admin.noName')}
                    </Link>
                  </p>
                  <p className="text-sm text-deep/70">
                    {t(`tripStatus.${trip.status}`)} ·{' '}
                    {t('admin.trips.seats', {
                      taken: asNumber(trip.seatsTaken),
                      seats: asNumber(trip.seats),
                    })}{' '}
                    · {formatMoney(trip.pricePerPerson, language)}
                  </p>
                </div>
                {live && (
                  <button
                    type="button"
                    className={dangerButtonClass}
                    onClick={() => setCancelling(trip)}
                  >
                    {t('admin.trips.cancel')}
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}

      {total > pageSize && (
        <nav className="flex justify-between" aria-label={t('common.pagination')}>
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            ← {t('common.previous')}
          </button>
          <button
            type="button"
            disabled={page * pageSize >= total}
            onClick={() => setPage((current) => current + 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            {t('common.next')} →
          </button>
        </nav>
      )}

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

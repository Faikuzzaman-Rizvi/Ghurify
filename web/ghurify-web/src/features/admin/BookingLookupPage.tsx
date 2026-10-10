import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { RefreshCw, Wallet } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type AdminBookingDetail } from './adminApi';
import { AdminPageHeader, AdminSearchBar, ListSkeleton } from './AdminUi';

/**
 * Answers "where is my money?": find a booking by its number or the payment reference the
 * traveller quotes, and see every payment, refund and what escrow still holds.
 */
export function BookingLookupPage() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const query = params.get('q') ?? '';
  const [draft, setDraft] = useState(query);

  const booking = useQuery({
    queryKey: ['admin', 'booking', query],
    queryFn: ({ signal }) => adminApi.lookupBooking(query, signal),
    enabled: query !== '',
    retry: false,
  });

  const retry = useMutation({
    mutationFn: () => adminApi.retryRefunds(),
    onSuccess: () => void booking.refetch(),
  });

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.operations')}
        title={t('admin.bookings.title')}
        description={t('admin.bookings.lead')}
        actions={
          <button
            type="button"
            className={secondaryButtonClass}
            disabled={retry.isPending}
            onClick={() => retry.mutate()}
          >
            <RefreshCw aria-hidden="true" className="h-4 w-4" />
            {t('admin.bookings.retryRefunds')}
          </button>
        }
      />
      {retry.isSuccess && (
        <p role="status" className="text-sm text-hill">
          {t('admin.bookings.retried', { count: asNumber(retry.data.count) })}
        </p>
      )}
      {retry.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(retry.error, t)}
        </p>
      )}

      <AdminSearchBar
        id="booking-lookup"
        label={t('admin.bookings.lookup')}
        placeholder={t('admin.bookings.lookupHint')}
        value={draft}
        onChange={setDraft}
        onSubmit={() => setParams(draft.trim() ? { q: draft.trim() } : {})}
        submitLabel={t('admin.bookings.lookup')}
      />

      {/* Before the first search: say what this page answers, instead of an empty canvas. */}
      {query === '' && (
        <div className="flex flex-col items-center gap-3 rounded-3xl border-2 border-dashed border-hill/15 bg-white/70 px-6 py-14 text-center">
          <span
            aria-hidden="true"
            className="flex h-16 w-16 items-center justify-center rounded-2xl bg-mist text-hill"
          >
            <Wallet className="h-8 w-8" />
          </span>
          <p className="font-display text-lg font-semibold text-deep">
            {t('admin.bookings.startTitle')}
          </p>
          <p className="max-w-md text-sm text-deep/65">{t('admin.bookings.startHint')}</p>
        </div>
      )}

      {booking.isFetching && <ListSkeleton rows={3} />}
      {booking.isError && (
        <ErrorState message={errorText(booking.error, t)} onRetry={() => void booking.refetch()} />
      )}
      {booking.data && !booking.isFetching && <BookingRecord booking={booking.data} />}
    </div>
  );
}

function BookingRecord({ booking }: { booking: AdminBookingDetail }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const when = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));
  const inEscrow = Number(booking.inEscrow ?? 0);

  return (
    <>
      <section className={`${cardClass} flex flex-col gap-2`}>
        <h3 className="text-lg font-bold text-deep">
          {t('admin.bookings.booking', { id: asNumber(booking.id) })} ·{' '}
          {t(`bookings.status.${booking.status}`)}
        </h3>
        <p className="text-sm text-deep/80">
          <Link to={`/trips/${asNumber(booking.tripId)}`} className="text-hill underline">
            {booking.tripTitle}
          </Link>{' '}
          · {formatDate(booking.startDate, language)}
        </p>
        <p className="text-sm text-deep/80">
          {t('admin.bookings.traveller')}:{' '}
          <Link
            to={`/admin/users/${asNumber(booking.travellerId)}`}
            className="text-hill underline"
          >
            {booking.travellerName ?? t('admin.noName')}
          </Link>{' '}
          · {t('admin.bookings.host')}:{' '}
          <Link to={`/admin/users/${asNumber(booking.hostId)}`} className="text-hill underline">
            {booking.hostName ?? t('admin.noName')}
          </Link>
        </p>
        <dl className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {(
            [
              ['held', booking.held],
              ['released', booking.released],
              ['refunded', booking.refunded],
              ['inEscrow', booking.inEscrow ?? 0],
            ] as const
          ).map(([key, value]) => (
            <div key={key} className="rounded-2xl bg-mist p-3">
              <dt className="text-xs text-deep/60">{t(`admin.bookings.ledger.${key}`)}</dt>
              <dd
                className={`text-lg font-bold ${key === 'inEscrow' && inEscrow < 0 ? 'text-jamdani' : 'text-deep'}`}
              >
                {money(value)}
              </dd>
            </div>
          ))}
        </dl>
      </section>

      <section className={cardClass} aria-labelledby="payments-title">
        <h3 id="payments-title" className="mb-3 text-lg font-bold text-deep">
          {t('admin.bookings.payments')}
        </h3>
        {booking.payments.length === 0 ? (
          <p className="text-sm text-deep/60">{t('admin.bookings.noPayments')}</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {booking.payments.map((payment) => (
              <li key={String(payment.id)} className="rounded-xl bg-mist p-3">
                <p className="font-semibold text-deep">
                  {money(payment.total)} ·{' '}
                  {t(`admin.bookings.paymentStatus.${payment.status ?? 'Created'}`)} ·{' '}
                  {payment.provider}
                </p>
                <p className="break-all text-deep/70">
                  {payment.transactionRef} · {when(payment.created)}
                  {payment.failureReason ? ` · ${payment.failureReason}` : ''}
                </p>
              </li>
            ))}
          </ul>
        )}

        <h3 className="mb-3 mt-5 text-lg font-bold text-deep">{t('admin.bookings.refunds')}</h3>
        {booking.refunds.length === 0 ? (
          <p className="text-sm text-deep/60">{t('admin.bookings.noRefunds')}</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {booking.refunds.map((refund) => (
              <li key={String(refund.id)} className="rounded-xl bg-mist p-3">
                <p
                  className={`font-semibold ${refund.status === 'Failed' ? 'text-jamdani' : 'text-deep'}`}
                >
                  {money(refund.amount)} · {t(`admin.bookings.refundStatus.${refund.status}`)} ·{' '}
                  {t(`admin.bookings.refundReason.${refund.reason}`)}
                </p>
                <p className="break-all text-deep/70">
                  {refund.reference} · {when(refund.created)} ·{' '}
                  {t('admin.bookings.attempts', { count: asNumber(refund.attempts) })}
                  {refund.failureReason ? ` · ${refund.failureReason}` : ''}
                </p>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { RefreshCw, Search } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, inputClass, secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type AdminBookingDetail } from './adminApi';

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
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.bookings.title')}
        </h2>
        <button
          type="button"
          className={secondaryButtonClass}
          disabled={retry.isPending}
          onClick={() => retry.mutate()}
        >
          <RefreshCw aria-hidden="true" className="h-4 w-4" />
          {t('admin.bookings.retryRefunds')}
        </button>
      </header>
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

      <form
        role="search"
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setParams(draft.trim() ? { q: draft.trim() } : {});
        }}
      >
        <label htmlFor="booking-lookup" className="sr-only">
          {t('admin.bookings.lookup')}
        </label>
        <input
          id="booking-lookup"
          type="search"
          value={draft}
          placeholder={t('admin.bookings.lookupHint')}
          onChange={(event) => setDraft(event.target.value)}
          className={`${inputClass} flex-1`}
        />
        <button
          type="submit"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep"
        >
          <Search aria-hidden="true" className="h-4 w-4" />
          {t('admin.bookings.lookup')}
        </button>
      </form>

      {booking.isFetching && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
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

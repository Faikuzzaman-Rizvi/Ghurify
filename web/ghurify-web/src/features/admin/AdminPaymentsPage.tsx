import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { Search } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { PaymentMethod, PaymentStatusBadge } from '@/features/payments/PaymentParts';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type PaymentStatus } from './adminApi';

const statuses: readonly (PaymentStatus | '')[] = [
  '',
  'Succeeded',
  'Pending',
  'Created',
  'Failed',
  'Expired',
];

/**
 * Every payment on the platform, for support and disputes: find one by the reference a traveller
 * quotes, the gateway's id, their email or name, or the trip, and open it in full.
 */
export function AdminPaymentsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const [params, setParams] = useSearchParams();
  const search = params.get('q') ?? '';
  const [draft, setDraft] = useState(search);
  const [status, setStatus] = useState<PaymentStatus | ''>('');
  const [page, setPage] = useState(1);

  const payments = useQuery({
    queryKey: ['admin', 'payments', search, status, page],
    queryFn: ({ signal }) => adminApi.payments(search, status, page, signal),
    placeholderData: (previous) => previous,
  });

  const total = payments.data ? asNumber(payments.data.totalCount) : 0;
  const pageSize = payments.data ? asNumber(payments.data.pageSize) : 25;
  const totals = payments.data?.totals;

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
        {t('admin.payments.title')}
      </h2>

      <form
        role="search"
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setParams(draft.trim() ? { q: draft.trim() } : {});
          setPage(1);
        }}
      >
        <label htmlFor="payment-search" className="sr-only">
          {t('admin.payments.search')}
        </label>
        <input
          id="payment-search"
          type="search"
          value={draft}
          placeholder={t('admin.payments.searchHint')}
          onChange={(event) => setDraft(event.target.value)}
          className={`${inputClass} flex-1`}
        />
        <label htmlFor="payment-status" className="sr-only">
          {t('admin.payments.status')}
        </label>
        <Select
          id="payment-status"
          className="sm:w-48"
          value={status}
          onChange={(value) => {
            setStatus(value as PaymentStatus | '');
            setPage(1);
          }}
          options={statuses.map((option) => ({
            value: option,
            label: option ? t(`paymentHistory.status.${option}`) : t('admin.payments.allStatuses'),
          }))}
          buttonClassName={`${inputClass} cursor-pointer`}
        />
        <button
          type="submit"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep"
        >
          <Search aria-hidden="true" className="h-4 w-4" />
          {t('admin.payments.search')}
        </button>
      </form>

      {totals && (
        <p className="text-sm text-deep/70">
          {t('admin.payments.totals', {
            count: asNumber(totals.count),
            formatted: formatCount(totals.count, language),
            paid: money(totals.paid),
            refunded: money(totals.refunded),
          })}
        </p>
      )}

      {payments.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {payments.isError && (
        <ErrorState
          message={errorText(payments.error, t)}
          onRetry={() => void payments.refetch()}
        />
      )}
      {payments.data?.items.length === 0 && <EmptyState title={t('admin.payments.empty')} />}

      {payments.data && payments.data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {payments.data.items.map((payment) => (
            <li
              key={String(payment.id)}
              className={`${cardClass} flex flex-wrap items-center justify-between gap-3 p-4!`}
            >
              <div className="min-w-0">
                <p className="flex flex-wrap items-center gap-2">
                  <Link
                    to={`/admin/payments/${asNumber(payment.id)}`}
                    className="font-semibold text-deep hover:underline"
                  >
                    #{asNumber(payment.id)} · {payment.tripTitle}
                  </Link>
                  <PaymentStatusBadge status={payment.status} />
                </p>
                <p className="text-sm text-deep/70">
                  <Link
                    to={`/admin/users/${asNumber(payment.travellerId)}`}
                    className="text-hill underline"
                  >
                    {payment.travellerName ?? t('admin.noName')}
                  </Link>{' '}
                  · {formatDateTime(payment.created, language)} ·{' '}
                  <PaymentMethod
                    type={payment.methodType}
                    name={payment.methodName}
                    last4={payment.accountLast4}
                  />
                </p>
                <p className="break-all font-mono text-xs text-deep/60">
                  {payment.transactionRef}
                  {payment.providerTxnId ? ` · ${payment.providerTxnId}` : ''}
                </p>
              </div>
              <div className="text-right">
                <p className="font-display text-lg font-bold text-hill">
                  {money(payment.paidAmount ?? payment.total)}
                </p>
                {asNumber(payment.refunded) > 0 && (
                  <p className="text-xs font-semibold text-river">
                    {t('paymentHistory.refundedAmount', { amount: money(payment.refunded) })}
                  </p>
                )}
              </div>
            </li>
          ))}
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
    </div>
  );
}

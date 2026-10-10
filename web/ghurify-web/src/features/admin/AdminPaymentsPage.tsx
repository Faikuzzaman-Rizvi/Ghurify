import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { Banknote, ChevronRight, Receipt, Undo2 } from 'lucide-react';

import { asNumber } from '@/api/client';
import { inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { PaymentMethod, PaymentStatusBadge } from '@/features/payments/PaymentParts';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type PaymentStatus } from './adminApi';
import { AdminPageHeader, AdminPager, AdminSearchBar, ListSkeleton, SummaryTiles } from './AdminUi';
import { adminPanelClass, adminRowClass } from './adminStyles';

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
 *
 * A ledger, so a list: one row per payment, the amounts lined up on the right.
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
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.operations')}
        title={t('admin.payments.title')}
        description={t('admin.payments.lead')}
      />

      <AdminSearchBar
        id="payment-search"
        label={t('admin.payments.search')}
        placeholder={t('admin.payments.searchHint')}
        value={draft}
        onChange={setDraft}
        onSubmit={() => {
          setParams(draft.trim() ? { q: draft.trim() } : {});
          setPage(1);
        }}
        submitLabel={t('admin.payments.search')}
      >
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
      </AdminSearchBar>

      {totals && (
        <>
          <SummaryTiles
            items={[
              {
                label: t('admin.payments.stats.count'),
                value: formatCount(totals.count, language),
                icon: Receipt,
              },
              {
                label: t('admin.payments.stats.paid'),
                value: money(totals.paid),
                icon: Banknote,
                tone: 'good',
              },
              {
                label: t('admin.payments.stats.refunded'),
                value: money(totals.refunded),
                icon: Undo2,
                tone: 'info',
              },
            ]}
          />
          {/* The same figures as one sentence, for anyone reading the page top to bottom. */}
          <p className="sr-only">
            {t('admin.payments.totals', {
              count: asNumber(totals.count),
              formatted: formatCount(totals.count, language),
              paid: money(totals.paid),
              refunded: money(totals.refunded),
            })}
          </p>
        </>
      )}

      {payments.isPending && <ListSkeleton />}
      {payments.isError && (
        <ErrorState
          message={errorText(payments.error, t)}
          onRetry={() => void payments.refetch()}
        />
      )}
      {payments.data?.items.length === 0 && <EmptyState title={t('admin.payments.empty')} />}

      {payments.data && payments.data.items.length > 0 && (
        <ul className={adminPanelClass}>
          {payments.data.items.map((payment) => (
            <li
              key={String(payment.id)}
              className={`${adminRowClass} flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-4 transition hover:bg-mist/50 sm:px-6`}
            >
              <span
                aria-hidden="true"
                className="hidden h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-mist text-hill sm:flex"
              >
                <Receipt className="h-5 w-5" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2">
                  <Link
                    to={`/admin/payments/${asNumber(payment.id)}`}
                    className="font-semibold text-deep underline-offset-4 hover:text-hill hover:underline"
                  >
                    #{asNumber(payment.id)} · {payment.tripTitle}
                  </Link>
                  <PaymentStatusBadge status={payment.status} />
                </p>
                <p className="mt-0.5 text-sm text-deep/65">
                  <Link
                    to={`/admin/users/${asNumber(payment.travellerId)}`}
                    className="font-medium text-hill underline-offset-4 hover:underline"
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
                <p className="mt-1 break-all font-mono text-xs text-deep/50">
                  {payment.transactionRef}
                  {payment.providerTxnId ? ` · ${payment.providerTxnId}` : ''}
                </p>
              </div>
              <div className="ml-auto text-right">
                <p className="font-display text-lg font-semibold text-deep">
                  {money(payment.paidAmount ?? payment.total)}
                </p>
                {asNumber(payment.refunded) > 0 && (
                  <p className="text-xs font-semibold text-river">
                    {t('paymentHistory.refundedAmount', { amount: money(payment.refunded) })}
                  </p>
                )}
              </div>
              <Link
                to={`/admin/payments/${asNumber(payment.id)}`}
                aria-hidden="true"
                tabIndex={-1}
                className="hidden text-deep/30 transition hover:text-hill sm:block"
              >
                <ChevronRight className="h-5 w-5" />
              </Link>
            </li>
          ))}
        </ul>
      )}

      <AdminPager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  );
}

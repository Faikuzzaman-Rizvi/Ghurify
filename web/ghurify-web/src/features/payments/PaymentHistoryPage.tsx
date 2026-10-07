import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Banknote, Compass, ReceiptText, Undo2, Wallet, type LucideIcon } from 'lucide-react';

import { asNumber } from '@/api/client';
import { accentButtonClass, cardClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Pagination } from '@/components/ui/Pagination';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { CopyButton, PaymentMethod, PaymentStatusBadge } from './PaymentParts';
import type { PaymentStatus } from './paymentsApi';
import { usePaymentHistory } from './usePayments';

const filters: readonly (PaymentStatus | '')[] = ['', 'Succeeded', 'Pending', 'Failed', 'Expired'];

/**
 * Every payment the traveller has made or tried, newest first: what they paid, how, the
 * transaction ID to quote, where it stands and what came back. Each opens as a receipt.
 */
export function PaymentHistoryPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const [status, setStatus] = useState<PaymentStatus | ''>('');
  const [page, setPage] = useState(1);
  const history = usePaymentHistory(status, page);

  const totals = history.data?.totals;
  const figures: { key: string; icon: LucideIcon; amount: number | string; tone: string }[] = [
    { key: 'paid', icon: Banknote, amount: totals?.paid ?? 0, tone: 'bg-hill text-white' },
    { key: 'refunded', icon: Undo2, amount: totals?.refunded ?? 0, tone: 'bg-sky text-river' },
    { key: 'net', icon: Wallet, amount: totals?.net ?? 0, tone: 'bg-turmeric/20 text-ochre' },
  ];

  const items = history.data?.items ?? [];
  const pages = history.data
    ? Math.ceil(asNumber(history.data.totalCount) / asNumber(history.data.pageSize))
    : 0;

  return (
    <>
      <PageBanner
        compact
        slug="coxs-bazar"
        kind="Beach"
        eyebrow={t('paymentHistory.eyebrow')}
        titleKey="paymentHistory.titleAccent"
        aside={
          <Link to="/me/trips" className={accentButtonClass}>
            <Compass aria-hidden="true" className="h-4 w-4" />
            {t('nav.myTrips')}
          </Link>
        }
      >
        <p className="mt-3 max-w-xl text-white/80">{t('paymentHistory.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 flex flex-col gap-8 pb-8">
        <ul className="grid gap-4 sm:grid-cols-3">
          {figures.map(({ key, icon: Icon, amount, tone }) => (
            <li key={key} className={`${cardClass} flex items-center gap-4 p-5!`}>
              <span
                className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${tone}`}
              >
                <Icon aria-hidden="true" className="h-6 w-6" />
              </span>
              <span className="min-w-0">
                <span className="block font-display text-2xl font-bold text-deep">
                  {history.isPending ? '—' : money(amount)}
                </span>
                <span className="block text-sm text-deep/60">
                  {t(`paymentHistory.figures.${key}`)}
                </span>
              </span>
            </li>
          ))}
        </ul>

        <section aria-labelledby="payments-heading" className={`${cardClass} overflow-hidden p-0!`}>
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-hill/10 px-6 py-5">
            <h2 id="payments-heading" className="text-lg font-semibold">
              {t('paymentHistory.list')}
              {totals && (
                <span className="ml-2 text-sm font-normal text-deep/60">
                  {t('paymentHistory.count', {
                    count: asNumber(totals.count),
                    formatted: formatCount(totals.count, language),
                  })}
                </span>
              )}
            </h2>
            <div
              role="group"
              aria-label={t('paymentHistory.filters.label')}
              className="flex flex-wrap gap-2"
            >
              {filters.map((filter) => (
                <button
                  key={filter || 'all'}
                  type="button"
                  aria-pressed={status === filter}
                  onClick={() => {
                    setStatus(filter);
                    setPage(1);
                  }}
                  className={`rounded-full px-3.5 py-1.5 text-sm font-semibold transition ${
                    status === filter
                      ? 'bg-hill text-white'
                      : 'border border-hill/15 text-deep hover:bg-hill/10'
                  }`}
                >
                  {t(`paymentHistory.filters.${filter || 'all'}`)}
                </button>
              ))}
            </div>
          </div>

          {history.isPending && (
            <div role="status" className="flex flex-col gap-3 p-6">
              <span className="sr-only">{t('common.loading')}</span>
              {[0, 1, 2].map((row) => (
                <div
                  key={row}
                  aria-hidden="true"
                  className="h-20 animate-pulse rounded-xl bg-hill/10"
                />
              ))}
            </div>
          )}
          {history.isError && (
            <div className="p-6">
              <ErrorState
                message={errorText(history.error, t)}
                onRetry={() => void history.refetch()}
              />
            </div>
          )}
          {history.data && items.length === 0 && (
            <div className="p-6">
              {status ? (
                <EmptyState title={t('paymentHistory.emptyFiltered')} />
              ) : (
                <EmptyState
                  title={t('paymentHistory.empty')}
                  hint={t('paymentHistory.emptyHint')}
                  action={
                    <Link to="/trips" className={accentButtonClass}>
                      {t('paymentHistory.findTrip')}
                    </Link>
                  }
                />
              )}
            </div>
          )}

          {items.length > 0 && (
            <ul className="divide-y divide-hill/10">
              {items.map((payment) => {
                const id = asNumber(payment.id);
                const refunded = asNumber(payment.refunded);
                return (
                  <li
                    key={String(payment.id)}
                    className="grid gap-3 px-6 py-5 transition hover:bg-mist/60 sm:grid-cols-[1fr_auto] sm:items-center"
                  >
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-2">
                        <Link
                          to={`/me/payments/${id}`}
                          className="truncate font-semibold text-deep hover:text-hill"
                        >
                          {payment.tripTitle}
                        </Link>
                        <PaymentStatusBadge status={payment.status} />
                      </p>
                      <p className="mt-1 text-sm text-deep/60">
                        <time dateTime={payment.created}>
                          {formatDateTime(payment.created, language)}
                        </time>{' '}
                        ·{' '}
                        <PaymentMethod
                          type={payment.methodType}
                          name={payment.methodName}
                          last4={payment.accountLast4}
                        />
                      </p>
                      <p className="mt-1 flex flex-wrap items-center gap-1 text-xs text-deep/60">
                        <span>{t('paymentHistory.transactionId')}:</span>
                        <span className="break-all font-mono">{payment.transactionRef}</span>
                        <CopyButton
                          value={payment.transactionRef}
                          label={t('paymentHistory.transactionId')}
                        />
                      </p>
                    </div>
                    <div className="flex items-center justify-between gap-4 sm:flex-col sm:items-end sm:gap-1">
                      <p className="font-display text-xl font-bold text-hill">
                        {money(payment.paidAmount ?? payment.total)}
                      </p>
                      {refunded > 0 && (
                        <p className="text-xs font-semibold text-river">
                          {t('paymentHistory.refundedAmount', { amount: money(refunded) })}
                        </p>
                      )}
                      <Link
                        to={`/me/payments/${id}`}
                        className="inline-flex items-center gap-1 text-sm font-semibold text-hill hover:underline"
                      >
                        <ReceiptText aria-hidden="true" className="h-4 w-4" />
                        {t('paymentHistory.viewReceipt')}
                      </Link>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>

        <Pagination page={page} pages={pages} onChange={setPage} label={t('common.pagination')} />
      </div>
    </>
  );
}

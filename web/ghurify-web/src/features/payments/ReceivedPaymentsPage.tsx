import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { Armchair, Banknote, Undo2, Wallet, X, type LucideIcon } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Pagination } from '@/components/ui/Pagination';
import { errorText } from '@/lib/errors';
import { formatCount, formatDateTime, formatMoney, toLanguage } from '@/lib/format';
import { CopyButton } from './PaymentParts';
import { useReceivedPayments } from './usePayments';

/**
 * The seats travellers have paid for on the host's trips, newest first, for one trip with
 * `?trip=ID`. Hosts see what each seat is worth and what went back, never how anyone paid.
 */
export function ReceivedPaymentsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const money = (value: number | string) => formatMoney(value, language);
  const [params, setParams] = useSearchParams();
  const tripId = Number(params.get('trip')) || null;
  const [page, setPage] = useState(1);
  const received = useReceivedPayments(tripId, page);

  const totals = received.data?.totals;
  const figures: { key: string; icon: LucideIcon; value: string; tone: string }[] = [
    {
      key: 'count',
      icon: Armchair,
      value: formatCount(totals?.count ?? 0, language),
      tone: 'bg-hill text-white',
    },
    {
      key: 'value',
      icon: Banknote,
      value: money(totals?.bookingValue ?? 0),
      tone: 'bg-emerald-100 text-emerald-800',
    },
    {
      key: 'refunded',
      icon: Undo2,
      value: money(totals?.refunded ?? 0),
      tone: 'bg-sky text-river',
    },
  ];

  const items = received.data?.items ?? [];
  const pages = received.data
    ? Math.ceil(asNumber(received.data.totalCount) / asNumber(received.data.pageSize))
    : 0;

  return (
    <>
      <PageBanner
        compact
        slug="kuakata"
        kind="Beach"
        eyebrow={t('hosting.title')}
        titleKey="received.titleAccent"
        aside={
          <Link
            to="/host/payouts"
            className="inline-flex items-center gap-2 rounded-full border border-white/40 px-5 py-3 text-sm font-semibold text-white backdrop-blur transition hover:bg-white/15"
          >
            <Wallet aria-hidden="true" className="h-4 w-4" />
            {t('received.payouts')}
          </Link>
        }
      >
        <p className="mt-3 max-w-xl text-white/80">{t('received.subtitle')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 flex flex-col gap-8 pb-8">
        <ul className="grid gap-4 sm:grid-cols-3">
          {figures.map(({ key, icon: Icon, value, tone }) => (
            <li key={key} className={`${cardClass} flex items-center gap-4 p-5!`}>
              <span
                className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${tone}`}
              >
                <Icon aria-hidden="true" className="h-6 w-6" />
              </span>
              <span className="min-w-0">
                <span className="block font-display text-2xl font-bold text-deep">
                  {received.isPending ? '—' : value}
                </span>
                <span className="block text-sm text-deep/60">{t(`received.figures.${key}`)}</span>
              </span>
            </li>
          ))}
        </ul>

        <section aria-labelledby="received-heading" className={`${cardClass} overflow-hidden p-0!`}>
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-hill/10 px-6 py-5">
            <h2 id="received-heading" className="text-lg font-semibold">
              {t('received.list')}
            </h2>
            {tripId && (
              <button
                type="button"
                onClick={() => {
                  setParams({});
                  setPage(1);
                }}
                className="inline-flex items-center gap-1.5 rounded-full bg-mist px-3.5 py-1.5 text-sm font-semibold text-deep transition hover:bg-hill/10"
              >
                {t('received.oneTrip')}
                <X aria-hidden="true" className="h-4 w-4" />
                <span className="sr-only">{t('received.showAll')}</span>
              </button>
            )}
          </div>

          {received.isPending && (
            <div role="status" className="flex flex-col gap-3 p-6">
              <span className="sr-only">{t('common.loading')}</span>
              {[0, 1, 2].map((row) => (
                <div
                  key={row}
                  aria-hidden="true"
                  className="h-16 animate-pulse rounded-xl bg-hill/10"
                />
              ))}
            </div>
          )}
          {received.isError && (
            <div className="p-6">
              <ErrorState
                message={errorText(received.error, t)}
                onRetry={() => void received.refetch()}
              />
            </div>
          )}
          {received.data && items.length === 0 && (
            <div className="p-6">
              <EmptyState title={t('received.empty')} hint={t('received.emptyHint')} />
            </div>
          )}

          {items.length > 0 && (
            <ul className="divide-y divide-hill/10">
              {items.map((payment) => {
                const refunded = asNumber(payment.refunded);
                return (
                  <li
                    key={String(payment.id)}
                    className="grid gap-3 px-6 py-5 transition hover:bg-mist/60 sm:grid-cols-[1fr_auto] sm:items-center"
                  >
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-deep">
                        <Link
                          to={`/host/trips/${asNumber(payment.tripId)}/requests`}
                          className="hover:text-hill"
                        >
                          {payment.tripTitle}
                        </Link>
                      </p>
                      <p className="mt-1 text-sm text-deep/70">
                        <Link
                          to={`/users/${asNumber(payment.travellerId)}`}
                          className="font-medium text-hill hover:underline"
                        >
                          {payment.travellerName ?? t('received.traveller')}
                        </Link>{' '}
                        · {t('receipt.bookingNumber', { id: asNumber(payment.bookingId) })}
                        {payment.paidOn && (
                          <>
                            {' '}
                            ·{' '}
                            <time dateTime={payment.paidOn}>
                              {formatDateTime(payment.paidOn, language)}
                            </time>
                          </>
                        )}
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
                        {money(payment.amount)}
                      </p>
                      {refunded > 0 && (
                        <p className="text-xs font-semibold text-river">
                          {t('received.refunded', { amount: money(refunded) })}
                        </p>
                      )}
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

import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowLeft,
  Banknote,
  CircleCheck,
  Clock,
  Landmark,
  PlaneTakeoff,
  ReceiptText,
  Route,
  type LucideIcon,
} from 'lucide-react';

import { apiGet, asNumber } from '@/api/client';
import type { components } from '@/api/schema';
import { cardClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { useAuthStore } from '@/features/auth/authStore';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';

type PayoutView = components['schemas']['PayoutView'];

const stageIcon: Record<PayoutView['stage'], LucideIcon> = {
  BeforeDeparture: PlaneTakeoff,
  AfterStart: Route,
};

/** What escrow has released to the host, trip by trip and stage by stage. */
export function HostPayoutsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');

  const payouts = useQuery({
    queryKey: ['host', user, 'payouts'],
    queryFn: ({ signal }) => apiGet<PayoutView[]>('/api/v1/me/payouts', { signal }),
  });

  const list = payouts.data ?? [];
  const sum = (items: PayoutView[]) => items.reduce((total, p) => total + asNumber(p.amount), 0);
  const paid = sum(list.filter((payout) => payout.status === 'Paid'));
  const sending = sum(list.filter((payout) => payout.status === 'Released'));

  const figures: { key: string; icon: LucideIcon; amount: number; tone: string }[] = [
    { key: 'total', icon: Banknote, amount: paid + sending, tone: 'bg-hill text-white' },
    { key: 'paid', icon: CircleCheck, amount: paid, tone: 'bg-emerald-100 text-emerald-800' },
    { key: 'sending', icon: Clock, amount: sending, tone: 'bg-turmeric/20 text-ochre' },
  ];

  return (
    <>
      <PageBanner
        compact
        slug="kuakata"
        kind="Beach"
        eyebrow={t('hosting.title')}
        titleKey="payouts.titleAccent"
        aside={
          <div className="flex flex-wrap gap-3">
            <Link
              to="/host/trips"
              className="inline-flex items-center gap-2 rounded-full border border-white/40 px-5 py-3 text-sm font-semibold text-white backdrop-blur transition hover:bg-white/15"
            >
              <ArrowLeft aria-hidden="true" className="h-4 w-4" />
              {t('hosting.myTrips')}
            </Link>
            <Link
              to="/host/payments"
              className="inline-flex items-center gap-2 rounded-full border border-white/40 px-5 py-3 text-sm font-semibold text-white backdrop-blur transition hover:bg-white/15"
            >
              <ReceiptText aria-hidden="true" className="h-4 w-4" />
              {t('received.link')}
            </Link>
          </div>
        }
      >
        <p className="mt-3 max-w-xl text-white/80">{t('payouts.subtitle')}</p>
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
                  {payouts.isPending ? '—' : formatMoney(amount, language)}
                </span>
                <span className="block text-sm text-deep/60">{t(`payouts.figures.${key}`)}</span>
              </span>
            </li>
          ))}
        </ul>

        <div className="grid items-start gap-8 lg:grid-cols-[1fr_20rem]">
          <section
            aria-labelledby="payouts-heading"
            className={`${cardClass} p-0! overflow-hidden`}
          >
            <h2
              id="payouts-heading"
              className="border-b border-hill/10 px-6 py-5 text-lg font-semibold"
            >
              {t('payouts.history')}
            </h2>

            {payouts.isPending && (
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
            {payouts.isError && (
              <div className="p-6">
                <ErrorState
                  message={errorText(payouts.error, t)}
                  onRetry={() => void payouts.refetch()}
                />
              </div>
            )}
            {payouts.data?.length === 0 && (
              <div className="p-6">
                <EmptyState title={t('payouts.empty')} hint={t('payouts.emptyHint')} />
              </div>
            )}

            {list.length > 0 && (
              <ul className="divide-y divide-hill/10">
                {list.map((payout) => {
                  const Icon = stageIcon[payout.stage];
                  return (
                    <li
                      key={String(payout.id)}
                      className="flex flex-wrap items-center gap-4 px-6 py-5 transition hover:bg-mist/60"
                    >
                      <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-mist text-hill">
                        <Icon aria-hidden="true" className="h-5 w-5" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-semibold text-deep">
                          <Link
                            to={`/trips/${asNumber(payout.tripId)}`}
                            className="hover:text-hill"
                          >
                            {payout.tripTitle}
                          </Link>
                        </p>
                        <p className="text-sm text-deep/60">
                          {t(`payouts.stage.${payout.stage}`)} ·{' '}
                          {formatDate(payout.created.slice(0, 10), language)}
                        </p>
                      </div>
                      <div className="text-right">
                        <p className="font-display text-xl font-bold text-hill">
                          {formatMoney(payout.amount, language)}
                        </p>
                        <span
                          className={`mt-1 inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                            payout.status === 'Paid'
                              ? 'bg-emerald-50 text-emerald-800'
                              : 'bg-turmeric/15 text-ochre'
                          }`}
                        >
                          {t(`payouts.status.${payout.status}`)}
                        </span>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </section>

          <aside className={`${cardClass} lg:sticky lg:top-24`}>
            <h2 className="flex items-center gap-3 text-lg font-semibold">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-turmeric text-night">
                <Landmark aria-hidden="true" className="h-5 w-5" />
              </span>
              {t('payouts.howTitle')}
            </h2>
            <ol className="mt-5 flex flex-col gap-5">
              {(['BeforeDeparture', 'AfterStart'] as const).map((stage, index) => {
                const Icon = stageIcon[stage];
                return (
                  <li key={stage} className="flex gap-3">
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-hill text-sm font-bold text-white">
                      {index + 1}
                    </span>
                    <div>
                      <p className="flex items-center gap-1.5 font-semibold text-deep">
                        <Icon aria-hidden="true" className="h-4 w-4 text-hill" />
                        {t(`payouts.stage.${stage}`)}
                      </p>
                      <p className="mt-1 text-sm text-deep/70">{t(`payouts.how.${stage}`)}</p>
                    </div>
                  </li>
                );
              })}
            </ol>
          </aside>
        </div>
      </div>
    </>
  );
}

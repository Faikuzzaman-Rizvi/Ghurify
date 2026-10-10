import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { CalendarDays, CircleCheck, Send } from 'lucide-react';

import { asNumber } from '@/api/client';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type PayoutStatus, type PayoutView } from './adminApi';
import { AdminPageHeader, CardGridSkeleton, FilterChips, StatusPill } from './AdminUi';
import { adminCardClass, smallPrimaryButtonClass } from './adminStyles';

const statuses: readonly PayoutStatus[] = ['Released', 'Paid'];

/**
 * Money released from escrow, waiting for the finance desk to send it to the host. Approving one
 * records that it was sent (audited); the host then sees it as paid.
 */
export function PayoutQueuePage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [status, setStatus] = useState<PayoutStatus>('Released');

  const queue = useQuery({
    queryKey: ['admin', 'payouts', status],
    queryFn: ({ signal }) => adminApi.payouts(status, signal),
  });

  const items = queue.data ?? [];
  const sum = items.reduce((total, payout) => total + asNumber(payout.amount), 0);

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.operations')}
        title={t('admin.payouts.title')}
        description={
          items.length > 0
            ? t(`admin.payouts.summary.${status}`, {
                count: items.length,
                amount: formatMoney(sum, language),
              })
            : t('admin.payouts.lead')
        }
        actions={
          <FilterChips
            label={t('admin.payouts.filter')}
            value={status}
            onChange={setStatus}
            options={statuses.map((option) => ({
              value: option,
              label: t(`admin.payouts.status.${option}`),
              tone: option === 'Released' ? ('warn' as const) : ('good' as const),
            }))}
          />
        }
      />

      {queue.isPending && <CardGridSkeleton count={3} className="h-64" />}

      {queue.isError && (
        <ErrorState message={errorText(queue.error, t)} onRetry={() => void queue.refetch()} />
      )}

      {queue.data?.length === 0 && <EmptyState title={t('admin.payouts.empty')} />}

      {items.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {items.map((payout) => (
            <li key={String(payout.id)} className="flex">
              <PayoutCard payout={payout} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function PayoutCard({ payout }: { payout: PayoutView }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const host = payout.hostName ?? t('admin.noName');

  const approve = useMutation({
    mutationFn: () => adminApi.approvePayout(asNumber(payout.id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'payouts'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    },
  });

  return (
    <article className={`${adminCardClass} p-5`}>
      <div className="flex min-w-0 items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-linear-to-br from-hill to-deep font-display text-base font-semibold text-white"
        >
          {host.trim().charAt(0).toUpperCase()}
        </span>
        <div className="min-w-0">
          <h3 className="truncate font-display text-base font-semibold text-deep">{host}</h3>
          <p className="line-clamp-1 text-sm text-deep/65">{payout.tripTitle}</p>
        </div>
      </div>
      <StatusPill tone="info" className="mt-3 self-start">
        {t(`payouts.stage.${payout.stage}`)}
      </StatusPill>

      <div className="my-5 rounded-2xl bg-mist/70 px-4 py-3.5 ring-1 ring-hill/8">
        <p className="font-display text-3xl font-semibold tracking-tight text-hill">
          {formatMoney(payout.amount, language)}
        </p>
        {Number(payout.platformAmount) > 0 && (
          <p className="mt-0.5 text-xs text-deep/60">
            {t('admin.payouts.fees', { amount: formatMoney(payout.platformAmount, language) })}
          </p>
        )}
      </div>

      <p className="flex items-center gap-2 text-sm text-deep/65">
        <CalendarDays aria-hidden="true" className="h-4 w-4 text-hill/70" />
        {t('admin.payouts.tripStarts', { date: formatDate(payout.startDate, language) })}
      </p>

      <div className="mt-auto pt-4">
        {payout.status === 'Released' ? (
          <button
            type="button"
            className={`${smallPrimaryButtonClass} w-full py-2.5`}
            disabled={approve.isPending}
            onClick={() => approve.mutate()}
          >
            <Send aria-hidden="true" className="h-4 w-4" />
            {t('admin.payouts.approve')}
          </button>
        ) : (
          <StatusPill tone="good" icon={CircleCheck}>
            {payout.approvedOn
              ? t('admin.payouts.sentOn', { date: formatDate(payout.approvedOn, language) })
              : t('admin.payouts.status.Paid')}
          </StatusPill>
        )}
        {approve.isError && (
          <p role="alert" className="mt-2 text-sm text-jamdani">
            {errorText(approve.error, t)}
          </p>
        )}
      </div>
    </article>
  );
}

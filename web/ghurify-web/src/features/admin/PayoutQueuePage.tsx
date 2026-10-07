import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { asNumber } from '@/api/client';
import { cardClass, primaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type PayoutStatus, type PayoutView } from './adminApi';

const statuses: readonly PayoutStatus[] = ['Released', 'Paid'];

/**
 * Money released from escrow, waiting for the finance desk to send it to the host. Approving one
 * records that it was sent (audited); the host then sees it as paid.
 */
export function PayoutQueuePage() {
  const { t } = useTranslation();
  const [status, setStatus] = useState<PayoutStatus>('Released');

  const queue = useQuery({
    queryKey: ['admin', 'payouts', status],
    queryFn: ({ signal }) => adminApi.payouts(status, signal),
  });

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.payouts.title')}
        </h2>
        <div
          role="group"
          aria-label={t('admin.payouts.filter')}
          className="flex gap-1 rounded-full bg-white p-1 shadow-sm ring-1 ring-hill/10"
        >
          {statuses.map((option) => (
            <button
              key={option}
              type="button"
              aria-pressed={status === option}
              onClick={() => setStatus(option)}
              className={`rounded-full px-4 py-1.5 text-sm font-semibold transition ${
                status === option
                  ? 'bg-hill text-white shadow-sm'
                  : 'text-deep/70 hover:bg-mist hover:text-deep'
              }`}
            >
              {t(`admin.payouts.status.${option}`)}
            </button>
          ))}
        </div>
      </header>

      {queue.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {queue.isError && (
        <ErrorState message={errorText(queue.error, t)} onRetry={() => void queue.refetch()} />
      )}

      {queue.data?.length === 0 && <EmptyState title={t('admin.payouts.empty')} />}

      {queue.data && queue.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {queue.data.map((payout) => (
            <li key={String(payout.id)}>
              <PayoutRow payout={payout} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function PayoutRow({ payout }: { payout: PayoutView }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();

  const approve = useMutation({
    mutationFn: () => adminApi.approvePayout(asNumber(payout.id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'payouts'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    },
  });

  return (
    <article className={`${cardClass} flex flex-wrap items-center justify-between gap-3`}>
      <div>
        <p className="font-bold text-deep">
          {payout.hostName ?? t('admin.noName')} · {payout.tripTitle}
        </p>
        <p className="text-sm text-deep/70">
          {t(`payouts.stage.${payout.stage}`)} · {formatDate(payout.startDate, language)}
        </p>
        <p className="mt-1 text-lg font-extrabold text-hill">
          {formatMoney(payout.amount, language)}
          {Number(payout.platformAmount) > 0 && (
            <span className="ml-2 text-xs font-normal text-deep/60">
              {t('admin.payouts.fees', { amount: formatMoney(payout.platformAmount, language) })}
            </span>
          )}
        </p>
      </div>
      {payout.status === 'Released' && (
        <button
          type="button"
          className={primaryButtonClass}
          disabled={approve.isPending}
          onClick={() => approve.mutate()}
        >
          {t('admin.payouts.approve')}
        </button>
      )}
      {approve.isError && (
        <p role="alert" className="w-full text-sm text-jamdani">
          {errorText(approve.error, t)}
        </p>
      )}
    </article>
  );
}

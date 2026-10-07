import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { asNumber } from '@/api/client';
import {
  cardClass,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { adminApi, type ReportAction, type ReportKind, type ReportView } from './adminApi';

/** What a moderator may do with each kind of report. The API checks the same pairing. */
const actionsFor: Record<ReportKind, readonly ReportAction[]> = {
  User: ['SuspendUser', 'Resolve', 'Dismiss'],
  Post: ['HidePost', 'Resolve', 'Dismiss'],
  Trip: ['Resolve', 'Dismiss'],
  Dispute: ['RefundBooking', 'Resolve', 'Dismiss'],
};

/**
 * The moderation queue: reports about people, stories and trips, or (with `disputes`) booking
 * disputes, which only an admin decides. Every decision needs a written reason and is audited.
 */
export function ReportsQueuePage({ disputes = false }: { disputes?: boolean }) {
  const { t } = useTranslation();

  const queue = useQuery({
    queryKey: ['admin', 'reports', disputes ? 'Dispute' : 'all'],
    queryFn: ({ signal }) => adminApi.reports(disputes ? 'Dispute' : null, signal),
    select: (reports) =>
      disputes ? reports : reports.filter((report) => report.kind !== 'Dispute'),
  });

  const title = disputes ? t('admin.reports.disputesTitle') : t('admin.reports.title');

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">{title}</h2>

      {queue.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {queue.isError && (
        <ErrorState message={errorText(queue.error, t)} onRetry={() => void queue.refetch()} />
      )}

      {queue.data && queue.data.length === 0 && <EmptyState title={t('admin.reports.empty')} />}

      {queue.data && queue.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {queue.data.map((report) => (
            <li key={String(report.id)}>
              <ReportRow report={report} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ReportRow({ report }: { report: ReportView }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [resolution, setResolution] = useState('');
  const id = asNumber(report.id);
  const targetId = asNumber(report.targetId);

  const decide = useMutation({
    mutationFn: (action: ReportAction) =>
      adminApi.resolveReport(id, { action, resolution: resolution.trim() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'reports'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    },
  });

  const target =
    report.kind === 'User' ? (
      <Link to={`/users/${targetId}`} className="font-semibold text-hill underline">
        {t('admin.reports.targetUser', { id: targetId })}
      </Link>
    ) : report.kind === 'Trip' ? (
      <Link to={`/trips/${targetId}`} className="font-semibold text-hill underline">
        {t('admin.reports.targetTrip', { id: targetId })}
      </Link>
    ) : report.kind === 'Post' ? (
      <span className="font-semibold">{t('admin.reports.targetPost', { id: targetId })}</span>
    ) : (
      <span className="font-semibold">{t('admin.reports.targetBooking', { id: targetId })}</span>
    );

  return (
    <article className={cardClass}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="text-deep">
          <span className="rounded-full bg-jamdani/10 px-2 py-0.5 text-xs font-semibold text-jamdani">
            {t(`admin.reports.reasons.${report.reason}`)}
          </span>{' '}
          {target}
        </p>
        <p className="text-xs text-deep/60">
          {new Intl.DateTimeFormat(i18n.language, {
            dateStyle: 'medium',
            timeStyle: 'short',
            timeZone: 'Asia/Dhaka',
          }).format(new Date(report.created))}
        </p>
      </div>
      <p className="mt-1 text-sm text-deep/70">
        {t('admin.reports.by', { name: report.reporterName ?? t('admin.noName') })}
      </p>
      {report.details && <p className="mt-2 whitespace-pre-line text-deep">{report.details}</p>}

      <div className="mt-3 flex flex-col gap-2">
        <label htmlFor={`resolution-${id}`} className="text-sm font-medium text-deep">
          {t('admin.reports.resolution')}
        </label>
        <input
          id={`resolution-${id}`}
          value={resolution}
          maxLength={500}
          onChange={(event) => setResolution(event.target.value)}
          className={inputClass}
        />
        <div className="flex flex-wrap gap-2">
          {actionsFor[report.kind].map((action) => (
            <button
              key={action}
              type="button"
              className={action === 'Dismiss' ? secondaryButtonClass : primaryButtonClass}
              disabled={decide.isPending || resolution.trim() === ''}
              onClick={() => decide.mutate(action)}
            >
              {t(`admin.reports.actions.${action}`)}
            </button>
          ))}
        </div>
      </div>
      {decide.isError && (
        <p role="alert" className="mt-2 text-sm text-jamdani">
          {errorText(decide.error, t)}
        </p>
      )}
    </article>
  );
}

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Clock, Newspaper, Receipt, Tent, UserRound, type LucideIcon } from 'lucide-react';

import { asNumber } from '@/api/client';
import { inputClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { adminApi, type ReportAction, type ReportKind, type ReportView } from './adminApi';
import { AdminPageHeader, ListSkeleton, StatusPill } from './AdminUi';
import {
  smallDangerButtonClass,
  smallPrimaryButtonClass,
  smallSecondaryButtonClass,
  toneTile,
  type Tone,
} from './adminStyles';

/** What a moderator may do with each kind of report. The API checks the same pairing. */
const actionsFor: Record<ReportKind, readonly ReportAction[]> = {
  User: ['SuspendUser', 'Resolve', 'Dismiss'],
  Post: ['HidePost', 'Resolve', 'Dismiss'],
  Trip: ['Resolve', 'Dismiss'],
  Dispute: ['RefundBooking', 'Resolve', 'Dismiss'],
};

/** Taking something away from somebody looks different from closing the report. */
const actionClass: Record<ReportAction, string> = {
  SuspendUser: smallDangerButtonClass,
  HidePost: smallDangerButtonClass,
  RefundBooking: smallPrimaryButtonClass,
  Resolve: smallPrimaryButtonClass,
  Dismiss: smallSecondaryButtonClass,
};

const kindIcon: Record<ReportKind, LucideIcon> = {
  User: UserRound,
  Post: Newspaper,
  Trip: Tent,
  Dispute: Receipt,
};

const kindTone: Record<ReportKind, Tone> = {
  User: 'bad',
  Post: 'warn',
  Trip: 'info',
  Dispute: 'neutral',
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
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.trust')}
        title={title}
        description={disputes ? t('admin.reports.disputesLead') : t('admin.reports.lead')}
        actions={
          queue.data &&
          queue.data.length > 0 && (
            <StatusPill tone="warn">
              {t('admin.reports.waiting', { count: queue.data.length })}
            </StatusPill>
          )
        }
      />

      {queue.isPending && <ListSkeleton rows={3} />}

      {queue.isError && (
        <ErrorState message={errorText(queue.error, t)} onRetry={() => void queue.refetch()} />
      )}

      {queue.data && queue.data.length === 0 && <EmptyState title={t('admin.reports.empty')} />}

      {queue.data && queue.data.length > 0 && (
        <ul className="flex flex-col gap-4">
          {queue.data.map((report) => (
            <li key={String(report.id)}>
              <ReportCard report={report} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ReportCard({ report }: { report: ReportView }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [resolution, setResolution] = useState('');
  const id = asNumber(report.id);
  const targetId = asNumber(report.targetId);
  const Icon = kindIcon[report.kind];

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
      <Link
        to={`/users/${targetId}`}
        className="font-semibold text-hill underline-offset-4 hover:underline"
      >
        {t('admin.reports.targetUser', { id: targetId })}
      </Link>
    ) : report.kind === 'Trip' ? (
      <Link
        to={`/trips/${targetId}`}
        className="font-semibold text-hill underline-offset-4 hover:underline"
      >
        {t('admin.reports.targetTrip', { id: targetId })}
      </Link>
    ) : report.kind === 'Post' ? (
      <span className="font-semibold text-deep">
        {t('admin.reports.targetPost', { id: targetId })}
      </span>
    ) : (
      <span className="font-semibold text-deep">
        {t('admin.reports.targetBooking', { id: targetId })}
      </span>
    );

  return (
    <article className="overflow-hidden rounded-3xl bg-white shadow-[0_6px_28px_rgba(15,42,31,0.07)] ring-1 ring-hill/10">
      <div className="flex flex-wrap items-start gap-4 p-5 sm:p-6">
        <span
          aria-hidden="true"
          className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl ${toneTile[kindTone[report.kind]]}`}
        >
          <Icon className="h-6 w-6" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill tone="bad">{t(`admin.reports.reasons.${report.reason}`)}</StatusPill>
            {target}
          </div>
          <p className="mt-1.5 text-sm text-deep/65">
            {t('admin.reports.by', { name: report.reporterName ?? t('admin.noName') })}
          </p>
        </div>
        <p className="flex items-center gap-1.5 text-xs text-deep/55">
          <Clock aria-hidden="true" className="h-3.5 w-3.5" />
          {new Intl.DateTimeFormat(i18n.language, {
            dateStyle: 'medium',
            timeStyle: 'short',
            timeZone: 'Asia/Dhaka',
          }).format(new Date(report.created))}
        </p>
        {report.details && (
          <blockquote className="w-full whitespace-pre-line rounded-2xl border-l-4 border-hill/25 bg-mist/60 px-4 py-3 text-deep">
            {report.details}
          </blockquote>
        )}
      </div>

      <div className="flex flex-col gap-3 border-t border-hill/8 bg-mist/40 p-5 sm:px-6">
        <label htmlFor={`resolution-${id}`} className="text-sm font-semibold text-deep">
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
              className={actionClass[action]}
              disabled={decide.isPending || resolution.trim() === ''}
              onClick={() => decide.mutate(action)}
            >
              {t(`admin.reports.actions.${action}`)}
            </button>
          ))}
        </div>
        {decide.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(decide.error, t)}
          </p>
        )}
      </div>
    </article>
  );
}

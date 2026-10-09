import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ArrowRight } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, inputClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { adminApi, type AuditEntry, type AuditFilter } from './adminApi';

const entityTypes = [
  '',
  'User',
  'StaffRole',
  'Verification',
  'Trip',
  'Report',
  'Post',
  'DestinationAlert',
  'SosEvent',
  'Payout',
  'Refund',
  'Destination',
  'EmergencyPoint',
] as const;

/**
 * The areas of the portal, as action prefixes. The API treats an action ending in a dot as
 * "every action in this group", so one list serves as a coarse filter without the screen having
 * to know every verb that exists.
 */
const areas = [
  '',
  'staff.',
  'user.',
  'role.',
  'verification.',
  'trip.',
  'destination.',
  'report.',
  'post.',
  'payout.',
  'refunds.',
  'sos.',
  'emergency_point.',
] as const;

const empty: AuditFilter = { actorId: '', action: '', entityType: '', entityId: '', from: '', to: '' };

/** Who did what, when, and what changed. Filtered by person, area, record or date. */
export function AuditLogPage() {
  const { t, i18n } = useTranslation();
  const [draft, setDraft] = useState<AuditFilter>(empty);
  const [filter, setFilter] = useState<AuditFilter>(empty);
  const [page, setPage] = useState(1);

  const entries = useQuery({
    queryKey: ['admin', 'audit', filter, page],
    queryFn: ({ signal }) => adminApi.auditLog(filter, page, signal),
  });

  const total = Number(entries.data?.total ?? 0);
  const pageSize = Number(entries.data?.pageSize ?? 50);
  const pages = Math.max(1, Math.ceil(total / pageSize));

  const when = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));

  function set<K extends keyof AuditFilter>(field: K, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
  }

  return (
    <div className="flex flex-col gap-4">
      <header>
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.audit.title')}
        </h2>
        <p className="mt-1 max-w-2xl text-sm text-deep/70">{t('admin.audit.lead')}</p>
      </header>

      <form
        className={`${cardClass} grid gap-3 sm:grid-cols-2 lg:grid-cols-3`}
        onSubmit={(event) => {
          event.preventDefault();
          setPage(1);
          setFilter({
            ...draft,
            // Ignore anything that is not an id, rather than ask the API about it.
            actorId: /^\d+$/.test(draft.actorId.trim()) ? draft.actorId.trim() : '',
            entityId: /^\d+$/.test(draft.entityId.trim()) ? draft.entityId.trim() : '',
          });
        }}
      >
        <Labelled id="audit-area" label={t('admin.audit.area')}>
          <Select
            id="audit-area"
            value={draft.action}
            onChange={(value) => set('action', value)}
            options={areas.map((area) => ({
              value: area,
              label: area ? t(`admin.audit.areas.${area.slice(0, -1)}`) : t('admin.audit.allAreas'),
            }))}
            buttonClassName={`${inputClass} cursor-pointer`}
          />
        </Labelled>

        <Labelled id="audit-type" label={t('admin.audit.entityType')}>
          <Select
            id="audit-type"
            value={draft.entityType}
            onChange={(value) => set('entityType', value)}
            options={entityTypes.map((type) => ({
              value: type,
              label: type || t('admin.audit.allTypes'),
            }))}
            buttonClassName={`${inputClass} cursor-pointer`}
          />
        </Labelled>

        <Labelled id="audit-id" label={t('admin.audit.entityId')}>
          <input
            id="audit-id"
            inputMode="numeric"
            value={draft.entityId}
            onChange={(event) => set('entityId', event.target.value)}
            className={inputClass}
          />
        </Labelled>

        <Labelled id="audit-actor" label={t('admin.audit.actorId')}>
          <input
            id="audit-actor"
            inputMode="numeric"
            value={draft.actorId}
            onChange={(event) => set('actorId', event.target.value)}
            className={inputClass}
          />
        </Labelled>

        <Labelled id="audit-from" label={t('admin.audit.from')}>
          <input
            id="audit-from"
            type="date"
            value={draft.from}
            onChange={(event) => set('from', event.target.value)}
            className={inputClass}
          />
        </Labelled>

        <Labelled id="audit-to" label={t('admin.audit.to')}>
          <input
            id="audit-to"
            type="date"
            value={draft.to}
            onChange={(event) => set('to', event.target.value)}
            className={inputClass}
          />
        </Labelled>

        <div className="flex flex-wrap items-end gap-2 sm:col-span-2 lg:col-span-3">
          <button type="submit" className={primaryButtonClass}>
            {t('admin.audit.filter')}
          </button>
          <button
            type="button"
            onClick={() => {
              setDraft(empty);
              setFilter(empty);
              setPage(1);
            }}
            className={secondaryButtonClass}
          >
            {t('admin.audit.clear')}
          </button>
          {entries.data && (
            <p className="ml-auto text-sm text-deep/60">
              {t('admin.audit.count', { count: total })}
            </p>
          )}
        </div>
      </form>

      {entries.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {entries.isError && (
        <ErrorState message={errorText(entries.error, t)} onRetry={() => void entries.refetch()} />
      )}
      {entries.data?.entries.length === 0 && <EmptyState title={t('admin.audit.empty')} />}

      {entries.data && entries.data.entries.length > 0 && (
        <>
          <ol className={`${cardClass} divide-y divide-hill/10 p-0!`}>
            {entries.data.entries.map((entry) => (
              <Entry key={String(entry.id)} entry={entry} when={when} />
            ))}
          </ol>

          {pages > 1 && (
            <nav className="flex items-center justify-between gap-3" aria-label={t('common.pages')}>
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((current) => current - 1)}
                className={`${secondaryButtonClass} disabled:opacity-50`}
              >
                {t('common.previous')}
              </button>
              <p className="text-sm text-deep/70">{t('common.pageOf', { page, pages })}</p>
              <button
                type="button"
                disabled={page >= pages}
                onClick={() => setPage((current) => current + 1)}
                className={`${secondaryButtonClass} disabled:opacity-50`}
              >
                {t('common.next')}
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  );
}

/** One entry, with the before/after pairs the use case recorded. */
function Entry({ entry, when }: { entry: AuditEntry; when: (iso: string) => string }) {
  const { t } = useTranslation();

  return (
    <li className="flex flex-col gap-1 px-4 py-3 text-sm sm:px-6">
      <p className="text-deep">
        <span className="font-mono text-xs font-semibold text-hill">{entry.action}</span> ·{' '}
        {entry.entityType} #
        {entry.entityType === 'User' ? (
          <Link to={`/admin/users/${asNumber(entry.entityId)}`} className="text-hill underline">
            {asNumber(entry.entityId)}
          </Link>
        ) : (
          asNumber(entry.entityId)
        )}
      </p>

      <p className="text-deep/70">
        <Link to={`/admin/users/${asNumber(entry.actorId)}`} className="text-hill underline">
          {entry.actorName ?? t('admin.noName')}
        </Link>{' '}
        · {when(entry.created)}
        {entry.note ? ` · ${entry.note}` : ''}
      </p>

      {entry.changes.length > 0 && (
        <ul className="mt-1 flex flex-col gap-0.5">
          {entry.changes.map((change) => (
            <li key={change.field} className="flex flex-wrap items-center gap-1.5 text-xs">
              <span className="font-semibold text-deep/70">{change.field}</span>
              <span className="text-deep/50 line-through">
                {change.from ?? t('admin.audit.nothing')}
              </span>
              <ArrowRight aria-hidden="true" className="h-3 w-3 text-deep/40" />
              <span className="font-medium text-deep">
                {change.to ?? t('admin.audit.nothing')}
              </span>
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}

function Labelled({
  id,
  label,
  children,
}: {
  id: string;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-xs font-semibold text-deep/70">
        {label}
      </label>
      {children}
    </div>
  );
}

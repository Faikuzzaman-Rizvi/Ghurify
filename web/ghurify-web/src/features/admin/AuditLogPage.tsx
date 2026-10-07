import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { asNumber } from '@/api/client';
import { cardClass, inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { adminApi } from './adminApi';

const entityTypes = [
  '',
  'User',
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

/** Who did what, when, and why: the newest 100 entries, optionally for one record. */
export function AuditLogPage() {
  const { t, i18n } = useTranslation();
  const [entityType, setEntityType] = useState('');
  const [entityId, setEntityId] = useState('');
  const [filter, setFilter] = useState({ entityType: '', entityId: '' });

  const entries = useQuery({
    queryKey: ['admin', 'audit', filter.entityType, filter.entityId],
    queryFn: ({ signal }) => adminApi.auditLog(filter.entityType, filter.entityId, signal),
  });

  const when = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Dhaka',
    }).format(new Date(iso));

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
        {t('admin.audit.title')}
      </h2>

      <form
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setFilter({ entityType, entityId: /^\d+$/.test(entityId.trim()) ? entityId.trim() : '' });
        }}
      >
        <label htmlFor="audit-type" className="sr-only">
          {t('admin.audit.entityType')}
        </label>
        <Select
          id="audit-type"
          className="sm:w-56"
          value={entityType}
          onChange={setEntityType}
          options={entityTypes.map((type) => ({
            value: type,
            label: type || t('admin.audit.allTypes'),
          }))}
          buttonClassName={`${inputClass} cursor-pointer`}
        />
        <label htmlFor="audit-id" className="sr-only">
          {t('admin.audit.entityId')}
        </label>
        <input
          id="audit-id"
          inputMode="numeric"
          value={entityId}
          placeholder={t('admin.audit.entityId')}
          onChange={(event) => setEntityId(event.target.value)}
          className={`${inputClass} sm:w-40`}
        />
        <button
          type="submit"
          className="rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep"
        >
          {t('admin.audit.filter')}
        </button>
      </form>

      {entries.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {entries.isError && (
        <ErrorState message={errorText(entries.error, t)} onRetry={() => void entries.refetch()} />
      )}
      {entries.data?.length === 0 && <EmptyState title={t('admin.audit.empty')} />}

      {entries.data && entries.data.length > 0 && (
        <ol className={`${cardClass} divide-y divide-hill/10 p-0!`}>
          {entries.data.map((entry) => (
            <li key={String(entry.id)} className="flex flex-col gap-0.5 px-4 py-3 text-sm sm:px-6">
              <p className="text-deep">
                <span className="font-mono text-xs font-semibold text-hill">{entry.action}</span> ·{' '}
                {entry.entityType} #
                {entry.entityType === 'User' ? (
                  <Link
                    to={`/admin/users/${asNumber(entry.entityId)}`}
                    className="text-hill underline"
                  >
                    {asNumber(entry.entityId)}
                  </Link>
                ) : (
                  asNumber(entry.entityId)
                )}
              </p>
              <p className="text-deep/70">
                <Link
                  to={`/admin/users/${asNumber(entry.actorId)}`}
                  className="text-hill underline"
                >
                  {entry.actorName ?? t('admin.noName')}
                </Link>{' '}
                · {when(entry.created)}
                {entry.note ? ` · ${entry.note}` : ''}
              </p>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

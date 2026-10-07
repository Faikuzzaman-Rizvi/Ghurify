import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Search } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { cardClass, inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { VerificationBadge } from '@/components/VerificationBadge';
import { errorText } from '@/lib/errors';
import { adminApi, type UserStatus } from './adminApi';
import { statusBadge } from './adminLabels';

const statuses: readonly (UserStatus | '')[] = [
  '',
  'Active',
  'Suspended',
  'Deactivated',
  'PendingEmail',
];

/** Find anyone by email, phone or name, and open their record. */
export function UsersPage() {
  const { t } = useTranslation();
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<UserStatus | ''>('');
  const [page, setPage] = useState(1);

  const users = useQuery({
    queryKey: ['admin', 'users', search, status, page],
    queryFn: ({ signal }) => adminApi.users(search, status, page, signal),
    placeholderData: (previous) => previous,
  });

  const total = users.data ? asNumber(users.data.totalCount) : 0;
  const pageSize = users.data ? asNumber(users.data.pageSize) : 25;

  return (
    <div className="flex flex-col gap-4">
      <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
        {t('admin.users.title')}
      </h2>

      <form
        role="search"
        className="flex flex-col gap-2 sm:flex-row"
        onSubmit={(event) => {
          event.preventDefault();
          setSearch(draft.trim());
          setPage(1);
        }}
      >
        <label htmlFor="user-search" className="sr-only">
          {t('admin.users.search')}
        </label>
        <input
          id="user-search"
          type="search"
          value={draft}
          placeholder={t('admin.users.searchHint')}
          onChange={(event) => setDraft(event.target.value)}
          className={`${inputClass} flex-1`}
        />
        <label htmlFor="user-status" className="sr-only">
          {t('admin.users.status')}
        </label>
        <Select
          id="user-status"
          className="sm:w-48"
          value={status}
          onChange={(value) => {
            setStatus(value as UserStatus | '');
            setPage(1);
          }}
          options={statuses.map((option) => ({
            value: option,
            label: option ? t(`admin.users.statuses.${option}`) : t('admin.users.allStatuses'),
          }))}
          buttonClassName={`${inputClass} cursor-pointer`}
        />
        <button
          type="submit"
          className="inline-flex items-center justify-center gap-2 rounded-full bg-hill px-6 py-3 font-semibold text-white shadow-sm transition hover:bg-deep"
        >
          <Search aria-hidden="true" className="h-4 w-4" />
          {t('admin.users.search')}
        </button>
      </form>

      {users.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {users.isError && (
        <ErrorState message={errorText(users.error, t)} onRetry={() => void users.refetch()} />
      )}
      {users.data?.items.length === 0 && <EmptyState title={t('admin.users.empty')} />}

      {users.data && users.data.items.length > 0 && (
        <ul className={`${cardClass} divide-y divide-hill/10 p-0!`}>
          {users.data.items.map((user) => (
            <li key={String(user.id)}>
              <Link
                to={`/admin/users/${asNumber(user.id)}`}
                className="flex flex-wrap items-center gap-3 px-4 py-3 hover:bg-mist sm:px-6"
              >
                <Avatar userId={asNumber(user.id)} name={user.displayName} size="sm" />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-deep">
                    {user.displayName ?? t('admin.noName')}
                  </p>
                  <p className="truncate text-sm text-deep/60">{user.email}</p>
                </div>
                <VerificationBadge level={user.verifiedLevel} />
                <span
                  className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusBadge[user.status]}`}
                >
                  {t(`admin.users.statuses.${user.status}`)}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {total > pageSize && (
        <nav className="flex justify-between" aria-label={t('common.pagination')}>
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            ← {t('common.previous')}
          </button>
          <button
            type="button"
            disabled={page * pageSize >= total}
            onClick={() => setPage((current) => current + 1)}
            className="inline-flex items-center gap-1 rounded-full border border-hill/15 bg-white px-4 py-2 text-sm font-semibold text-deep transition hover:bg-mist disabled:pointer-events-none disabled:opacity-40"
          >
            {t('common.next')} →
          </button>
        </nav>
      )}
    </div>
  );
}

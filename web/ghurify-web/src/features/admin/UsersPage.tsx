import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { ChevronRight } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { inputClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { EmptyState, ErrorState } from '@/components/States';
import { VerificationBadge } from '@/components/VerificationBadge';
import { errorText } from '@/lib/errors';
import { adminApi, type UserStatus } from './adminApi';
import { AdminPageHeader, AdminPager, AdminSearchBar, ListSkeleton, StatusPill } from './AdminUi';
import { adminPanelClass, adminRowClass, type Tone } from './adminStyles';

const statuses: readonly (UserStatus | '')[] = [
  '',
  'Active',
  'Suspended',
  'Deactivated',
  'PendingEmail',
];

const statusTone: Record<UserStatus, Tone> = {
  Active: 'good',
  Suspended: 'bad',
  Deactivated: 'neutral',
  PendingEmail: 'warn',
};

/**
 * Find anyone by email, phone or name, and open their record. A list rather than cards: the
 * desk scans down it for one person, and a row keeps every name in the same place.
 */
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
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.operations')}
        title={t('admin.users.title')}
        description={t('admin.users.lead')}
      />

      <AdminSearchBar
        id="user-search"
        label={t('admin.users.search')}
        placeholder={t('admin.users.searchHint')}
        value={draft}
        onChange={setDraft}
        onSubmit={() => {
          setSearch(draft.trim());
          setPage(1);
        }}
        submitLabel={t('admin.users.search')}
      >
        <label htmlFor="user-status" className="sr-only">
          {t('admin.users.status')}
        </label>
        <Select
          id="user-status"
          className="sm:w-52"
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
      </AdminSearchBar>

      {users.isPending && <ListSkeleton />}
      {users.isError && (
        <ErrorState message={errorText(users.error, t)} onRetry={() => void users.refetch()} />
      )}
      {users.data?.items.length === 0 && <EmptyState title={t('admin.users.empty')} />}

      {users.data && users.data.items.length > 0 && (
        <ul className={adminPanelClass}>
          {users.data.items.map((user) => (
            <li key={String(user.id)} className={adminRowClass}>
              <Link
                to={`/admin/users/${asNumber(user.id)}`}
                className="group flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3.5 transition hover:bg-mist/70 focus-visible:bg-mist focus-visible:outline-none sm:px-6"
              >
                {/* The version comes with the row, so a person with no picture costs no request
                    at all and a person with one is asked for at a URL the browser can cache.
                    Left out, every row here asked the avatar endpoint on every visit. */}
                <Avatar
                  userId={asNumber(user.id)}
                  name={user.displayName}
                  version={user.avatarVersion}
                  size="md"
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-deep group-hover:text-hill">
                    {user.displayName ?? t('admin.noName')}
                  </p>
                  <p className="truncate text-sm text-deep/60">{user.email}</p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <VerificationBadge level={user.verifiedLevel} />
                  <StatusPill tone={statusTone[user.status]}>
                    {t(`admin.users.statuses.${user.status}`)}
                  </StatusPill>
                </div>
                <ChevronRight
                  aria-hidden="true"
                  className="hidden h-5 w-5 text-deep/30 transition group-hover:translate-x-0.5 group-hover:text-hill sm:block"
                />
              </Link>
            </li>
          ))}
        </ul>
      )}

      <AdminPager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  );
}

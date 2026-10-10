import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { CalendarDays, Crown, Mail, Search, UserMinus, UserPlus } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { Dialog } from '@/components/Dialog';
import { inputClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { Select } from '@/components/ui/Select';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { adminApi, type StaffMemberView, type StaffRoleView } from './adminApi';
import { AdminPageHeader, CardGridSkeleton, StatusPill } from './AdminUi';
import { adminCardClass, type Tone } from './adminStyles';
import { usePermissions } from './permissions';
import { permissions as permissionKeys } from './permissions';
import { useStepUp } from './useStepUp';

/**
 * Who is on the admin desk: every staff member, the roles each holds, and who put them there.
 *
 * Granting and revoking both need the password again, and the API refuses the moves this screen
 * also hides: changing your own roles, handing over a role you do not hold in full, and taking
 * away the last active super admin.
 */
export function StaffMembersPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [adding, setAdding] = useState(false);
  const { can } = usePermissions();
  const mayAssign = can(permissionKeys.staffAssign);

  const members = useQuery({
    queryKey: ['admin', 'staff', 'members'],
    queryFn: ({ signal }) => adminApi.staffMembers(signal),
  });

  const roles = useQuery({
    queryKey: ['admin', 'staff', 'roles'],
    queryFn: ({ signal }) => adminApi.staffRoles(signal),
  });

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.system')}
        title={t('admin.staff.title')}
        description={t('admin.staff.lead')}
        actions={
          mayAssign && (
            <button type="button" onClick={() => setAdding(true)} className={primaryButtonClass}>
              <UserPlus aria-hidden="true" className="h-4 w-4" />
              {t('admin.staff.add')}
            </button>
          )
        }
      />

      {members.isPending && <CardGridSkeleton count={3} className="h-60" />}

      {members.isError && (
        <ErrorState message={errorText(members.error, t)} onRetry={() => void members.refetch()} />
      )}

      {members.data?.members.length === 0 && <EmptyState title={t('admin.staff.empty')} />}

      {members.data && members.data.members.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {members.data.members.map((member) => (
            <MemberCard
              key={String(member.userId)}
              member={member}
              language={language}
              mayAssign={mayAssign}
            />
          ))}
        </ul>
      )}

      {adding && roles.data && (
        <AddMemberDialog roles={roles.data.roles} onClose={() => setAdding(false)} />
      )}
    </div>
  );
}

function MemberCard({
  member,
  language,
  mayAssign,
}: {
  member: StaffMemberView;
  language: 'bn' | 'en';
  mayAssign: boolean;
}) {
  const { t, i18n } = useTranslation();
  const stepUp = useStepUp();
  const queryClient = useQueryClient();

  const revoke = useMutation({
    mutationFn: (staffRoleId: number) =>
      adminApi.assignStaffRole(asNumber(member.userId), staffRoleId, false),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'staff'] }),
  });

  const since = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'medium',
    timeZone: 'Asia/Dhaka',
  }).format(new Date(member.staffSince));

  return (
    <li className="flex">
      <article className={`${adminCardClass} gap-4 p-5`}>
        <div className="flex items-start justify-between gap-3">
          <Avatar
            userId={asNumber(member.userId)}
            name={member.displayName}
            version={member.avatarUpdatedOn}
            size="md"
            className="ring-4 ring-mist"
          />
          <StatusPill tone={statusTone[member.status]}>
            {t(`admin.users.statuses.${member.status}`)}
          </StatusPill>
        </div>

        <div className="min-w-0">
          <h3 className="font-display text-lg font-semibold leading-snug">
            <Link
              to={`/admin/users/${asNumber(member.userId)}`}
              className="text-deep underline-offset-4 hover:text-hill hover:underline"
            >
              {member.displayName ?? t('admin.noName')}
            </Link>
          </h3>
          <p className="mt-1 flex items-center gap-2 text-sm text-deep/65">
            <Mail aria-hidden="true" className="h-4 w-4 shrink-0 text-hill/70" />
            <span className="truncate">{member.email}</span>
          </p>
          <p className="mt-1 flex items-center gap-2 text-xs text-deep/55">
            <CalendarDays aria-hidden="true" className="h-4 w-4 shrink-0 text-hill/60" />
            {t('admin.staff.since', { date: since })}
          </p>
        </div>

        <ul className="mt-auto flex flex-wrap gap-2 border-t border-hill/8 pt-4">
          {member.roles.map((held) => (
            <li
              key={String(held.staffRoleId)}
              className="flex items-center gap-1.5 rounded-full bg-hill/8 py-1 pl-3 pr-1.5 text-sm font-medium text-deep"
            >
              {held.isSuperAdmin && (
                <Crown aria-hidden="true" className="h-3.5 w-3.5 text-turmeric" />
              )}
              <span>{language === 'bn' ? held.nameBn : held.name}</span>
              {held.grantedByName && (
                <span className="text-xs font-normal text-deep/50">
                  {t('admin.staff.grantedBy', { name: held.grantedByName })}
                </span>
              )}
              {mayAssign && (
                <button
                  type="button"
                  disabled={revoke.isPending}
                  onClick={() =>
                    void stepUp.run(() => revoke.mutateAsync(asNumber(held.staffRoleId)))
                  }
                  aria-label={t('admin.staff.revoke', {
                    role: language === 'bn' ? held.nameBn : held.name,
                    name: member.displayName ?? member.email,
                  })}
                  className="rounded-full p-1 text-deep/50 transition hover:bg-jamdani/10 hover:text-jamdani disabled:opacity-50"
                >
                  <UserMinus aria-hidden="true" className="h-3.5 w-3.5" />
                </button>
              )}
            </li>
          ))}
        </ul>

        {revoke.isError && (
          <p role="alert" className="text-sm font-medium text-jamdani">
            {errorText(revoke.error, t)}
          </p>
        )}

        {stepUp.dialog}
      </article>
    </li>
  );
}

const statusTone: Record<StaffMemberView['status'], Tone> = {
  Active: 'good',
  Suspended: 'bad',
  Deactivated: 'neutral',
  PendingEmail: 'warn',
};

/**
 * Puts somebody on the desk. The person is found by the same search the People screen uses, so
 * there is no second way to look accounts up.
 */
function AddMemberDialog({
  roles,
  onClose,
}: {
  roles: readonly StaffRoleView[];
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const stepUp = useStepUp();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [chosen, setChosen] = useState<number | null>(null);
  const [roleId, setRoleId] = useState(String(roles[0]?.id ?? ''));

  const found = useQuery({
    queryKey: ['admin', 'users', query, '', 1],
    queryFn: ({ signal }) => adminApi.users(query, '', 1, signal),
    enabled: query.length > 0,
  });

  const grant = useMutation({
    mutationFn: ({ userId, staffRoleId }: { userId: number; staffRoleId: number }) =>
      adminApi.assignStaffRole(userId, staffRoleId, true),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'staff'] });
      onClose();
    },
  });

  return (
    <Dialog open title={t('admin.staff.addTitle')} onClose={onClose}>
      <div className="flex flex-col gap-4">
        <form
          className="flex gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            setQuery(search.trim());
            setChosen(null);
          }}
        >
          <div className="min-w-0 flex-1">
            <label htmlFor="staff-search" className="sr-only">
              {t('admin.staff.search')}
            </label>
            <input
              id="staff-search"
              value={search}
              placeholder={t('admin.staff.search')}
              onChange={(event) => setSearch(event.target.value)}
              className={inputClass}
            />
          </div>
          <button type="submit" className={secondaryButtonClass}>
            <Search aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">{t('admin.staff.search')}</span>
          </button>
        </form>

        {found.isError && <p className="text-sm text-jamdani">{errorText(found.error, t)}</p>}

        {found.data && found.data.items.length === 0 && (
          <p className="text-sm text-deep/60">{t('admin.staff.noMatches')}</p>
        )}

        {found.data && found.data.items.length > 0 && (
          <ul className="max-h-48 divide-y divide-hill/10 overflow-y-auto rounded-xl border border-hill/15">
            {found.data.items.map((person) => (
              <li key={String(person.id)}>
                <label className="flex cursor-pointer items-center gap-3 px-3 py-2 text-sm hover:bg-mist">
                  <input
                    type="radio"
                    name="staff-person"
                    checked={chosen === asNumber(person.id)}
                    onChange={() => setChosen(asNumber(person.id))}
                    className="h-4 w-4 accent-hill"
                  />
                  <span className="min-w-0">
                    <span className="block font-medium text-deep">
                      {person.displayName ?? t('admin.noName')}
                    </span>
                    <span className="block truncate text-xs text-deep/60">{person.email}</span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
        )}

        <div className="flex flex-col gap-1.5">
          <label htmlFor="staff-role" className="text-sm font-semibold text-deep">
            {t('admin.staff.role')}
          </label>
          <Select
            id="staff-role"
            value={roleId}
            onChange={setRoleId}
            options={roles.map((role) => ({
              value: String(role.id),
              label: language === 'bn' ? role.nameBn : role.name,
            }))}
            buttonClassName={`${inputClass} cursor-pointer`}
          />
        </div>

        {grant.isError && (
          <p role="alert" className="text-sm font-medium text-jamdani">
            {errorText(grant.error, t)}
          </p>
        )}

        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" onClick={onClose} className={secondaryButtonClass}>
            {t('common.cancel')}
          </button>
          <button
            type="button"
            disabled={chosen === null || !roleId || grant.isPending}
            onClick={() =>
              void stepUp.run(() =>
                grant.mutateAsync({ userId: chosen!, staffRoleId: Number(roleId) }),
              )
            }
            className={`${primaryButtonClass} disabled:opacity-60`}
          >
            {grant.isPending ? t('common.saving') : t('admin.staff.grant')}
          </button>
        </div>
      </div>

      {stepUp.dialog}
    </Dialog>
  );
}

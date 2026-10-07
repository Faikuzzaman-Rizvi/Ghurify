import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { ArrowLeft } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { cardClass, dangerButtonClass, secondaryButtonClass } from '@/components/Field';
import { ErrorState } from '@/components/States';
import { VerificationBadge } from '@/components/VerificationBadge';
import { errorText } from '@/lib/errors';
import { formatDate, formatMoney, toLanguage } from '@/lib/format';
import { adminApi, type AdminUserDetail, type Role, type UserStatus } from './adminApi';
import { statusBadge } from './adminLabels';
import { ReasonDialog } from './ReasonDialog';

/** Roles an admin can grant here. Traveler is everyone's and cannot be removed. */
const grantable: readonly Role[] = [
  'Host',
  'Guide',
  'Operator',
  'Moderator',
  'SafetyDesk',
  'Admin',
];

type Pending = { kind: 'status'; status: UserStatus } | { kind: 'password' } | null;

/** One person, in full, with every action the desk can take on their account. */
export function UserDetailPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { id } = useParams();
  const userId = Number(id);

  const user = useQuery({
    queryKey: ['admin', 'user', userId],
    queryFn: ({ signal }) => adminApi.user(userId, signal),
    enabled: Number.isFinite(userId) && userId > 0,
  });

  return (
    <div className="flex flex-col gap-4">
      <Link
        to="/admin/users"
        className="inline-flex items-center gap-1 text-sm font-medium text-hill hover:underline"
      >
        <ArrowLeft aria-hidden="true" className="h-4 w-4" />
        {t('admin.users.title')}
      </Link>

      {user.isPending && (
        <div role="status" className="h-60 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {user.isError && (
        <ErrorState message={errorText(user.error, t)} onRetry={() => void user.refetch()} />
      )}
      {user.data && <UserRecord user={user.data} language={language} />}
    </div>
  );
}

function UserRecord({ user, language }: { user: AdminUserDetail; language: 'bn' | 'en' }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const id = asNumber(user.id);
  const [pending, setPending] = useState<Pending>(null);
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'user', id] });

  const setStatus = useMutation({
    mutationFn: ({ status, reason }: { status: UserStatus; reason: string }) =>
      adminApi.setUserStatus(id, status, reason),
    onSuccess: () => {
      setPending(null);
      void refresh();
      void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    },
  });
  const requireReset = useMutation({
    mutationFn: (reason: string) => adminApi.requirePasswordReset(id, reason),
    onSuccess: () => {
      setPending(null);
      void refresh();
    },
  });
  const changeRole = useMutation({
    mutationFn: ({ role, grant }: { role: Role; grant: boolean }) =>
      adminApi.changeRole(id, role, grant),
    onSuccess: () => void refresh(),
  });
  const removeAvatar = useMutation({
    mutationFn: () => adminApi.removeAvatar(id),
    onSuccess: () => void refresh(),
  });

  const date = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeZone: 'Asia/Dhaka' }).format(
      new Date(iso),
    );

  return (
    <>
      <section className={`${cardClass} flex flex-col gap-4 sm:flex-row sm:items-center`}>
        <Avatar userId={id} name={user.displayName} version={user.avatarVersion} size="lg" />
        <div className="min-w-0 flex-1">
          <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
            {user.displayName ?? t('admin.noName')}
          </h2>
          <p className="break-all text-sm text-deep/70">{user.email}</p>
          {user.phone && (
            <p className="text-sm text-deep/70">
              <a href={`tel:${user.phone}`} className="text-hill underline">
                {user.phone}
              </a>
            </p>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${statusBadge[user.status]}`}
            >
              {t(`admin.users.statuses.${user.status}`)}
            </span>
            <VerificationBadge
              level={user.verifications.find((check) => check.status === 'Approved')?.level ?? null}
            />
            <span className="text-xs text-deep/60">
              {t('admin.users.joined', { date: date(user.created) })}
            </span>
            {!user.hasPassword && (
              <span className="text-xs text-deep/60">· {t('admin.users.noPassword')}</span>
            )}
            {user.mustResetPassword && (
              <span className="text-xs text-jamdani">· {t('admin.users.mustReset')}</span>
            )}
          </div>
        </div>
      </section>

      <section className={cardClass} aria-labelledby="actions-title">
        <h3 id="actions-title" className="mb-3 text-lg font-bold text-deep">
          {t('admin.users.actions')}
        </h3>
        <div className="flex flex-wrap gap-2">
          {user.status === 'Active' ? (
            <button
              type="button"
              className={dangerButtonClass}
              onClick={() => setPending({ kind: 'status', status: 'Suspended' })}
            >
              {t('admin.users.suspend')}
            </button>
          ) : (
            user.status !== 'PendingEmail' && (
              <button
                type="button"
                className={secondaryButtonClass}
                onClick={() => setPending({ kind: 'status', status: 'Active' })}
              >
                {t('admin.users.reactivate')}
              </button>
            )
          )}
          {user.status !== 'Deactivated' && (
            <button
              type="button"
              className={dangerButtonClass}
              onClick={() => setPending({ kind: 'status', status: 'Deactivated' })}
            >
              {t('admin.users.close')}
            </button>
          )}
          <button
            type="button"
            className={secondaryButtonClass}
            onClick={() => setPending({ kind: 'password' })}
          >
            {t('admin.users.requireReset')}
          </button>
          {user.avatarVersion && (
            <button
              type="button"
              className={secondaryButtonClass}
              disabled={removeAvatar.isPending}
              onClick={() => removeAvatar.mutate()}
            >
              {t('admin.users.removePicture')}
            </button>
          )}
        </div>
        {removeAvatar.isError && (
          <p role="alert" className="mt-2 text-sm text-jamdani">
            {errorText(removeAvatar.error, t)}
          </p>
        )}

        <fieldset className="mt-5">
          <legend className="mb-2 text-sm font-semibold text-deep">{t('admin.users.roles')}</legend>
          <div className="flex flex-wrap gap-2">
            {grantable.map((role) => {
              const has = user.roles.includes(role);
              return (
                <label
                  key={role}
                  className={`flex cursor-pointer items-center gap-2 rounded-full px-3 py-1.5 text-sm ring-1 ${
                    has ? 'bg-hill/10 ring-hill/40' : 'ring-hill/15'
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={has}
                    disabled={changeRole.isPending}
                    onChange={(event) => changeRole.mutate({ role, grant: event.target.checked })}
                  />
                  {t(`roles.${role}`)}
                </label>
              );
            })}
          </div>
          {changeRole.isError && (
            <p role="alert" className="mt-2 text-sm text-jamdani">
              {errorText(changeRole.error, t)}
            </p>
          )}
        </fieldset>
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className={cardClass} aria-labelledby="checks-title">
          <h3 id="checks-title" className="mb-3 text-lg font-bold text-deep">
            {t('admin.users.checks')}
          </h3>
          {user.verifications.length === 0 ? (
            <p className="text-sm text-deep/60">{t('admin.users.noChecks')}</p>
          ) : (
            <ul className="flex flex-col gap-2 text-sm">
              {user.verifications.map((check) => (
                <li key={String(check.id)} className="rounded-xl bg-mist p-3">
                  <p className="font-semibold text-deep">
                    {t(`verification.levels.${check.level}`)} ·{' '}
                    {t(`verification.idTypes.${check.idType}`)} ·{' '}
                    {t(`verification.status.${check.status}`)}
                  </p>
                  <p className="text-deep/70">
                    {date(check.created)} · {check.provider} ·{' '}
                    {t('admin.verifications.photoCount', { count: asNumber(check.documentCount) })}
                    {check.reason ? ` · ${check.reason}` : ''}
                  </p>
                </li>
              ))}
            </ul>
          )}
          <p className="mt-3 text-sm">
            {t('admin.users.reports', {
              open: asNumber(user.openReports),
              total: asNumber(user.totalReports),
            })}{' '}
            <Link to="/admin/reports" className="text-hill underline">
              {t('admin.nav.reports')}
            </Link>
          </p>
        </section>

        <section className={cardClass} aria-labelledby="trips-title">
          <h3 id="trips-title" className="mb-3 text-lg font-bold text-deep">
            {t('admin.users.hostedTrips')}
          </h3>
          {user.hostedTrips.length === 0 ? (
            <p className="text-sm text-deep/60">{t('admin.users.noTrips')}</p>
          ) : (
            <ul className="flex flex-col gap-2 text-sm">
              {user.hostedTrips.map((trip) => (
                <li key={String(trip.id)} className="flex justify-between gap-2">
                  <Link to={`/trips/${asNumber(trip.id)}`} className="truncate text-hill underline">
                    {trip.title}
                  </Link>
                  <span className="shrink-0 text-deep/70">
                    {formatDate(trip.startDate, language)} · {t(`tripStatus.${trip.status}`)}
                  </span>
                </li>
              ))}
            </ul>
          )}

          <h3 className="mb-3 mt-5 text-lg font-bold text-deep">{t('admin.users.bookings')}</h3>
          {user.bookings.length === 0 ? (
            <p className="text-sm text-deep/60">{t('admin.users.noBookings')}</p>
          ) : (
            <ul className="flex flex-col gap-2 text-sm">
              {user.bookings.map((booking) => (
                <li key={String(booking.id)} className="flex justify-between gap-2">
                  <Link
                    to={`/admin/bookings?q=${asNumber(booking.id)}`}
                    className="truncate text-hill underline"
                  >
                    #{asNumber(booking.id)} · {booking.tripTitle}
                  </Link>
                  <span className="shrink-0 text-deep/70">
                    {formatMoney(booking.amount, language)} ·{' '}
                    {t(`bookings.status.${booking.status}`)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <ReasonDialog
        key={pending?.kind === 'status' ? pending.status : pending?.kind}
        open={pending !== null}
        title={
          pending?.kind === 'status'
            ? t(`admin.users.confirm.${pending.status}`, { name: user.displayName ?? '' })
            : t('admin.users.confirm.password', { name: user.displayName ?? '' })
        }
        description={
          pending?.kind === 'status' && pending.status !== 'Active'
            ? t('admin.users.confirm.signsOut')
            : undefined
        }
        confirmLabel={
          pending?.kind === 'status'
            ? t(`admin.users.confirm.button.${pending.status}`)
            : t('admin.users.requireReset')
        }
        danger={pending?.kind === 'status' && pending.status !== 'Active'}
        pending={setStatus.isPending || requireReset.isPending}
        error={setStatus.error ?? requireReset.error}
        onClose={() => {
          setStatus.reset();
          requireReset.reset();
          setPending(null);
        }}
        onConfirm={(reason) => {
          if (pending?.kind === 'status') setStatus.mutate({ status: pending.status, reason });
          else requireReset.mutate(reason);
        }}
      />
    </>
  );
}

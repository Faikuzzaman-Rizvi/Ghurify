import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowUpRight,
  BadgeCheck,
  CalendarCheck,
  CircleSlash,
  ClockAlert,
  Flag,
  RotateCcw,
  Scale,
  Siren,
  Tent,
  TriangleAlert,
  Wallet,
  type LucideIcon,
} from 'lucide-react';

import { ErrorState } from '@/components/States';
import type { Role } from '@/features/auth/profileApi';
import { useMyProfile } from '@/features/auth/useProfile';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage } from '@/lib/format';
import { adminApi, type DashboardCounts } from './adminApi';

interface Tile {
  key: keyof DashboardCounts;
  icon: LucideIcon;
  /** Where to act on it, for the roles that can. */
  to?: string;
  roles?: readonly Role[];
  /** Needs attention when above zero. */
  urgent?: boolean;
}

const tiles: readonly Tile[] = [
  { key: 'openSos', icon: Siren, to: '/admin/sos', roles: ['Admin', 'SafetyDesk'], urgent: true },
  {
    key: 'missedCheckIns',
    icon: ClockAlert,
    to: '/admin/sos',
    roles: ['Admin', 'SafetyDesk'],
    urgent: true,
  },
  { key: 'openReports', icon: Flag, to: '/admin/reports', roles: ['Admin', 'Moderator'] },
  { key: 'openDisputes', icon: Scale, to: '/admin/disputes', roles: ['Admin'] },
  { key: 'pendingVerifications', icon: BadgeCheck, to: '/admin/verifications', roles: ['Admin'] },
  { key: 'payoutsAwaitingApproval', icon: Wallet, to: '/admin/payouts', roles: ['Admin'] },
  { key: 'refundsInFlight', icon: RotateCcw },
  {
    key: 'closedDestinations',
    icon: CircleSlash,
    to: '/admin/destinations',
    roles: ['Admin', 'SafetyDesk'],
  },
  {
    key: 'cautionDestinations',
    icon: TriangleAlert,
    to: '/admin/destinations',
    roles: ['Admin', 'SafetyDesk'],
  },
  { key: 'liveTrips', icon: Tent },
  { key: 'bookingsConfirmed', icon: CalendarCheck },
];

/** What needs a person right now, at a glance, with a way to each queue the viewer can open. */
export function AdminDashboardPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data: profile } = useMyProfile();
  const roles = profile?.roles ?? [];

  const counts = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => adminApi.dashboard(signal),
    refetchInterval: 60_000,
  });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.dashboard.title')}
        </h2>
        <p className="mt-1 text-sm text-deep/60">{t('admin.dashboard.lead')}</p>
      </div>

      {counts.isPending && (
        <div role="status" className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <span className="sr-only">{t('common.loading')}</span>
          {[0, 1, 2, 3, 4, 5].map((index) => (
            <div
              key={index}
              aria-hidden="true"
              className="h-32 animate-pulse rounded-2xl bg-hill/10"
            />
          ))}
        </div>
      )}

      {counts.isError && (
        <ErrorState message={errorText(counts.error, t)} onRetry={() => void counts.refetch()} />
      )}

      {counts.data && (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          {tiles.map((tile) => {
            const Icon = tile.icon;
            const value = Number(counts.data[tile.key]);
            const hot = tile.urgent === true && value > 0;
            const canOpen = tile.to && tile.roles?.some((role) => roles.includes(role));
            const body = (
              <>
                <span className="flex items-start justify-between">
                  <span
                    className={`flex h-11 w-11 items-center justify-center rounded-xl ${
                      hot ? 'bg-jamdani text-white' : 'bg-hill/10 text-hill'
                    }`}
                  >
                    <Icon aria-hidden="true" className="h-5 w-5" />
                  </span>
                  {canOpen && (
                    <ArrowUpRight
                      aria-hidden="true"
                      className="h-5 w-5 text-deep/30 transition group-hover:text-hill"
                    />
                  )}
                </span>
                <span
                  className={`mt-4 block font-display text-3xl font-bold ${hot ? 'text-jamdani' : 'text-deep'}`}
                >
                  {formatCount(value, language)}
                </span>
                <span className="text-sm text-deep/70">{t(`admin.dashboard.${tile.key}`)}</span>
              </>
            );
            const tileClass = `group flex h-full flex-col rounded-2xl bg-white p-5 shadow-[0_4px_24px_rgba(15,42,31,0.06)] ring-1 transition ${
              hot ? 'ring-2 ring-jamdani/50' : 'ring-hill/10'
            }`;

            return (
              <li key={tile.key}>
                {canOpen ? (
                  <Link
                    to={tile.to!}
                    className={`${tileClass} hover:-translate-y-0.5 hover:shadow-lg hover:ring-hill/30`}
                  >
                    {body}
                  </Link>
                ) : (
                  <div className={tileClass}>{body}</div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

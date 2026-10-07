import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowUpRight,
  BadgeCheck,
  CalendarCheck,
  CircleCheck,
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
import { BangladeshMap } from '@/components/ui/BangladeshMap';
import type { Role } from '@/features/auth/profileApi';
import { useSignedInIdentity } from '@/features/auth/useAccount';
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

/** The figures, grouped by how soon a person should look at them. */
const sections: readonly { key: 'attention' | 'queues' | 'platform'; tiles: readonly Tile[] }[] = [
  {
    key: 'attention',
    tiles: [
      {
        key: 'openSos',
        icon: Siren,
        to: '/admin/sos',
        roles: ['Admin', 'SafetyDesk'],
        urgent: true,
      },
      {
        key: 'missedCheckIns',
        icon: ClockAlert,
        to: '/admin/sos',
        roles: ['Admin', 'SafetyDesk'],
        urgent: true,
      },
    ],
  },
  {
    key: 'queues',
    tiles: [
      { key: 'openReports', icon: Flag, to: '/admin/reports', roles: ['Admin', 'Moderator'] },
      { key: 'openDisputes', icon: Scale, to: '/admin/disputes', roles: ['Admin'] },
      {
        key: 'pendingVerifications',
        icon: BadgeCheck,
        to: '/admin/verifications',
        roles: ['Admin'],
      },
      { key: 'payoutsAwaitingApproval', icon: Wallet, to: '/admin/payouts', roles: ['Admin'] },
    ],
  },
  {
    key: 'platform',
    tiles: [
      { key: 'liveTrips', icon: Tent },
      { key: 'bookingsConfirmed', icon: CalendarCheck },
      { key: 'refundsInFlight', icon: RotateCcw },
      {
        key: 'cautionDestinations',
        icon: TriangleAlert,
        to: '/admin/destinations',
        roles: ['Admin', 'SafetyDesk'],
      },
      {
        key: 'closedDestinations',
        icon: CircleSlash,
        to: '/admin/destinations',
        roles: ['Admin', 'SafetyDesk'],
      },
    ],
  },
];

/** The counts that are work for someone: emergencies and the queues. */
const workKeys: readonly (keyof DashboardCounts)[] = [
  'openSos',
  'missedCheckIns',
  'openReports',
  'openDisputes',
  'pendingVerifications',
  'payoutsAwaitingApproval',
];

/** Morning, afternoon or evening, by the clock in Dhaka rather than the browser's. */
function partOfDay(): 'morning' | 'afternoon' | 'evening' {
  const hour = Number(
    new Intl.DateTimeFormat('en-GB', { hour: 'numeric', hour12: false, timeZone: 'Asia/Dhaka' })
      .format(new Date())
      .slice(0, 2),
  );
  return hour < 12 ? 'morning' : hour < 17 ? 'afternoon' : 'evening';
}

/** What needs a person right now, at a glance, with a way to each queue the viewer can open. */
export function AdminDashboardPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data: profile } = useMyProfile();
  const identity = useSignedInIdentity();
  const roles = profile?.roles ?? [];

  const counts = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => adminApi.dashboard(signal),
    refetchInterval: 60_000,
  });

  const waiting = counts.data
    ? workKeys.reduce((sum, key) => sum + Number(counts.data[key]), 0)
    : null;
  const today = new Intl.DateTimeFormat(language === 'bn' ? 'bn-BD' : 'en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'Asia/Dhaka',
  }).format(new Date());

  return (
    <div className="flex flex-col gap-8">
      {/* The welcome banner: deep green like the site's photo bands, with the country drawn in. */}
      <section className="relative isolate overflow-hidden rounded-3xl bg-linear-to-br from-night via-deep to-hill px-6 py-8 text-white shadow-[0_20px_50px_rgba(15,42,31,0.25)] sm:px-10 sm:py-10">
        <BangladeshMap
          tone="dark"
          showDestinations
          className="pointer-events-none absolute -right-10 -top-6 -z-10 h-[130%] w-auto opacity-70 sm:right-4"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-10 bg-linear-to-r from-night/90 via-night/40 to-transparent"
        />

        <p className="eyebrow text-dusk!">{today}</p>
        <h2 className="mt-3 max-w-xl font-display text-3xl font-semibold leading-tight text-white! sm:text-4xl">
          {t(`admin.dashboard.greeting.${partOfDay()}`, { name: identity.name })}
        </h2>
        <p className="mt-3 max-w-lg text-white/80">
          {waiting === null
            ? t('admin.dashboard.lead')
            : waiting === 0
              ? t('admin.dashboard.allClear')
              : t('admin.dashboard.waiting', {
                  count: waiting,
                  n: formatCount(waiting, language),
                })}
        </p>

        {counts.data && (
          <ul className="mt-7 flex flex-wrap gap-3">
            {(['liveTrips', 'bookingsConfirmed'] as const).map((key) => (
              <li
                key={key}
                className="flex items-baseline gap-2 rounded-full bg-white/10 px-4 py-2 text-sm ring-1 ring-white/15 backdrop-blur"
              >
                <span className="font-display text-lg font-bold text-white">
                  {formatCount(counts.data[key], language)}
                </span>
                <span className="text-white/75">{t(`admin.dashboard.chip.${key}`)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {counts.isPending && (
        <div role="status" className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <span className="sr-only">{t('common.loading')}</span>
          {[0, 1, 2, 3, 4, 5, 6, 7].map((index) => (
            <div
              key={index}
              aria-hidden="true"
              className="h-36 animate-pulse rounded-2xl bg-white/70 ring-1 ring-hill/10"
            />
          ))}
        </div>
      )}

      {counts.isError && (
        <ErrorState message={errorText(counts.error, t)} onRetry={() => void counts.refetch()} />
      )}

      {counts.data &&
        sections.map((section) => (
          <section key={section.key} aria-labelledby={`dash-${section.key}`}>
            <div className="mb-4 flex items-end justify-between gap-4">
              <div>
                <h3
                  id={`dash-${section.key}`}
                  className="font-display text-lg font-semibold text-deep"
                >
                  {t(`admin.dashboard.sections.${section.key}`)}
                </h3>
                <p className="text-sm text-deep/60">
                  {t(`admin.dashboard.sectionLead.${section.key}`)}
                </p>
              </div>
              {section.key === 'attention' &&
                section.tiles.every((tile) => Number(counts.data[tile.key]) === 0) && (
                  <span className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-800 ring-1 ring-emerald-600/20">
                    <CircleCheck aria-hidden="true" className="h-3.5 w-3.5" />
                    {t('admin.dashboard.calm')}
                  </span>
                )}
            </div>

            <ul
              className={`grid gap-4 ${
                section.key === 'attention'
                  ? 'grid-cols-2'
                  : section.key === 'queues'
                    ? 'grid-cols-2 lg:grid-cols-4'
                    : 'grid-cols-2 lg:grid-cols-5'
              }`}
            >
              {section.tiles.map((tile) => (
                <li key={tile.key}>
                  <DashboardTile
                    tile={tile}
                    value={Number(counts.data[tile.key])}
                    canOpen={Boolean(tile.to && tile.roles?.some((role) => roles.includes(role)))}
                    large={section.key === 'attention'}
                  />
                </li>
              ))}
            </ul>
          </section>
        ))}
    </div>
  );
}

function DashboardTile({
  tile,
  value,
  canOpen,
  large,
}: {
  tile: Tile;
  value: number;
  canOpen: boolean;
  large: boolean;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const Icon = tile.icon;
  const hot = tile.urgent === true && value > 0;

  const body = (
    <>
      {/* A faint oversized icon in the corner gives each card its own shape. */}
      <Icon
        aria-hidden="true"
        className={`pointer-events-none absolute -bottom-4 -right-4 h-24 w-24 ${
          hot ? 'text-white/10' : 'text-hill/[0.05]'
        }`}
      />
      <span className="flex items-start justify-between">
        <span
          className={`flex h-11 w-11 items-center justify-center rounded-xl ${
            hot
              ? 'bg-white/15 text-white ring-1 ring-white/25'
              : 'bg-linear-to-br from-hill/15 to-hill/5 text-hill'
          }`}
        >
          <Icon aria-hidden="true" className="h-5 w-5" />
        </span>
        {canOpen && (
          <ArrowUpRight
            aria-hidden="true"
            className={`h-5 w-5 transition group-hover:-translate-y-0.5 group-hover:translate-x-0.5 ${
              hot ? 'text-white/70' : 'text-deep/30 group-hover:text-hill'
            }`}
          />
        )}
      </span>
      <span
        className={`mt-5 block font-display font-bold leading-none ${large ? 'text-5xl' : 'text-3xl'} ${
          hot ? 'text-white' : 'text-deep'
        }`}
      >
        {formatCount(value, language)}
      </span>
      <span className={`mt-2 text-sm ${hot ? 'text-white/85' : 'text-deep/70'}`}>
        {t(`admin.dashboard.${tile.key}`)}
      </span>
    </>
  );

  const tileClass = `group relative isolate flex h-full flex-col overflow-hidden rounded-2xl p-5 transition ${
    large ? 'sm:p-6' : ''
  } ${
    hot
      ? 'bg-linear-to-br from-jamdani to-[#7d2245] shadow-[0_14px_34px_rgba(163,48,92,0.35)]'
      : 'bg-white/85 shadow-[0_4px_24px_rgba(15,42,31,0.06)] ring-1 ring-hill/10 backdrop-blur'
  }`;

  return canOpen ? (
    <Link
      to={tile.to!}
      className={`${tileClass} hover:-translate-y-0.5 ${hot ? '' : 'hover:shadow-lg hover:ring-hill/30'}`}
    >
      {body}
    </Link>
  ) : (
    <div className={tileClass}>{body}</div>
  );
}

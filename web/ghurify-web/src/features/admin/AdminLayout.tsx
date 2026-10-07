import { useEffect, useId, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link, NavLink, Outlet, useLocation } from 'react-router';
import {
  Ambulance,
  ArrowUpRight,
  BadgeCheck,
  ChevronRight,
  Flag,
  LayoutDashboard,
  MapPinned,
  Menu,
  Receipt,
  Scale,
  ScrollText,
  Siren,
  Tent,
  Users,
  Wallet,
  X,
  type LucideIcon,
} from 'lucide-react';
import { LanguageToggle } from '@/components/LanguageToggle';
import { Logo } from '@/components/Logo';
import { NotificationBell } from '@/components/NotificationBell';
import { AccountMenu } from '@/components/SiteHeader';
import type { Role } from '@/features/auth/profileApi';
import { useMyProfile } from '@/features/auth/useProfile';
import { formatCount, toLanguage } from '@/lib/format';
import { adminApi, type DashboardCounts } from './adminApi';

interface Section {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Roles that may open it. The API enforces the same rule on every call. */
  roles: readonly Role[];
  /** A dashboard count shown beside the link while above zero: work waiting in that queue. */
  count?: keyof DashboardCounts;
  /** The count is an emergency, not just a backlog. */
  urgent?: boolean;
}

interface Group {
  key: string;
  sections: readonly Section[];
}

const staff: readonly Role[] = ['Admin', 'SafetyDesk', 'Moderator'];
const safety: readonly Role[] = ['Admin', 'SafetyDesk'];

const groups: readonly Group[] = [
  {
    key: 'overview',
    sections: [{ to: '/admin', label: 'admin.nav.dashboard', icon: LayoutDashboard, roles: staff }],
  },
  {
    key: 'safety',
    sections: [
      {
        to: '/admin/sos',
        label: 'admin.nav.sos',
        icon: Siren,
        roles: safety,
        count: 'openSos',
        urgent: true,
      },
      {
        to: '/admin/destinations',
        label: 'admin.nav.destinations',
        icon: MapPinned,
        roles: safety,
      },
      { to: '/admin/emergency-points', label: 'admin.nav.points', icon: Ambulance, roles: safety },
    ],
  },
  {
    key: 'trust',
    sections: [
      {
        to: '/admin/reports',
        label: 'admin.nav.reports',
        icon: Flag,
        roles: ['Admin', 'Moderator'],
        count: 'openReports',
      },
      {
        to: '/admin/disputes',
        label: 'admin.nav.disputes',
        icon: Scale,
        roles: ['Admin'],
        count: 'openDisputes',
      },
      {
        to: '/admin/verifications',
        label: 'admin.nav.verifications',
        icon: BadgeCheck,
        roles: ['Admin'],
        count: 'pendingVerifications',
      },
    ],
  },
  {
    key: 'operations',
    sections: [
      { to: '/admin/users', label: 'admin.nav.users', icon: Users, roles: ['Admin'] },
      { to: '/admin/trips', label: 'admin.nav.trips', icon: Tent, roles: ['Admin'] },
      { to: '/admin/bookings', label: 'admin.nav.bookings', icon: Receipt, roles: ['Admin'] },
      {
        to: '/admin/payouts',
        label: 'admin.nav.payouts',
        icon: Wallet,
        roles: ['Admin'],
        count: 'payoutsAwaitingApproval',
      },
    ],
  },
  {
    key: 'system',
    sections: [
      { to: '/admin/audit', label: 'admin.nav.audit', icon: ScrollText, roles: ['Admin'] },
    ],
  },
];

/** The groups and links this person's roles can open, in order. */
function useVisibleGroups() {
  const { data: profile } = useMyProfile();
  const roles = profile?.roles ?? [];

  return groups
    .map((group) => ({
      ...group,
      sections: group.sections.filter((section) =>
        section.roles.some((role) => roles.includes(role)),
      ),
    }))
    .filter((group) => group.sections.length > 0);
}

/** The section the address is in: the longest matching link, so /admin/users/7 is "Users". */
function useCurrentSection(): Section | undefined {
  const { pathname } = useLocation();
  return groups
    .flatMap((group) => group.sections)
    .filter((section) =>
      section.to === '/admin'
        ? pathname === '/admin' || pathname === '/admin/'
        : pathname.startsWith(section.to),
    )
    .sort((a, b) => b.to.length - a.to.length)[0];
}

/**
 * The admin portal's frame, separate from the public site: a full-height sidebar of the
 * sections the person's roles can open (grouped by the kind of work, with live counts of what
 * is waiting), a top bar with where they are and their account, and the page.
 */
export function AdminLayout() {
  const { t } = useTranslation();
  const current = useCurrentSection();
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Any navigation closes the drawer (reset while rendering, not in an effect).
  const { key: locationKey } = useLocation();
  const [seenLocation, setSeenLocation] = useState(locationKey);
  if (seenLocation !== locationKey) {
    setSeenLocation(locationKey);
    setDrawerOpen(false);
  }

  return (
    <div className="min-h-svh bg-mist lg:pl-72">
      <aside className="fixed inset-y-0 left-0 z-40 hidden w-72 lg:block">
        <Sidebar />
      </aside>

      <MobileDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />

      <header className="sticky top-0 z-30 border-b border-hill/10 bg-white/90 backdrop-blur">
        <div className="flex h-16 items-center gap-3 px-4 sm:px-6 lg:px-8">
          <button
            type="button"
            onClick={() => setDrawerOpen(true)}
            aria-label={t('nav.openMenu')}
            aria-haspopup="dialog"
            className="rounded-lg p-2 text-deep transition hover:bg-mist lg:hidden"
          >
            <Menu aria-hidden="true" className="h-5 w-5" />
          </button>

          <nav aria-label={t('trip.breadcrumb')} className="min-w-0 flex-1">
            <ol className="flex items-center gap-1.5 text-sm">
              <li className="hidden sm:block">
                <Link to="/admin" className="text-deep/60 transition hover:text-hill">
                  {t('admin.title')}
                </Link>
              </li>
              {current && current.to !== '/admin' && (
                <>
                  <li aria-hidden="true" className="hidden text-deep/30 sm:block">
                    <ChevronRight className="h-4 w-4" />
                  </li>
                  <li className="truncate font-semibold text-deep" aria-current="page">
                    {t(current.label)}
                  </li>
                </>
              )}
              {current?.to === '/admin' && (
                <li className="font-semibold text-deep sm:hidden" aria-current="page">
                  {t(current.label)}
                </li>
              )}
            </ol>
          </nav>

          <div className="flex items-center gap-1.5 text-deep sm:gap-2">
            <Link
              to="/"
              className="hidden items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium text-deep/70 transition hover:bg-mist hover:text-deep md:inline-flex"
            >
              {t('admin.portal.backToSite')}
              <ArrowUpRight aria-hidden="true" className="h-4 w-4" />
            </Link>
            <LanguageToggle compact />
            <NotificationBell />
            <AccountMenu glass={false} />
          </div>
        </div>
      </header>

      <main className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        {/* Each page titles itself with an h2; the portal is the page's h1. */}
        <h1 className="sr-only">{t('admin.portal.title')}</h1>
        <div className="mx-auto max-w-6xl">
          <Outlet />
        </div>
      </main>
    </div>
  );
}

/** The dark navigation column: brand, grouped sections with counts, and who is signed in. */
function Sidebar({ onClose }: { onClose?: () => void }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const visible = useVisibleGroups();
  const { data: profile } = useMyProfile();
  const staffRoles = (profile?.roles ?? []).filter((role) => staff.includes(role));

  // Shares the dashboard's query and cache: one request a minute feeds both.
  const counts = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => adminApi.dashboard(signal),
    refetchInterval: 60_000,
    enabled: staffRoles.length > 0,
  });

  return (
    <div className="flex h-full flex-col bg-night text-white">
      <div className="flex h-16 shrink-0 items-center justify-between gap-2 border-b border-white/10 px-5">
        <Link to="/admin" className="flex items-center gap-2 rounded-xl">
          <Logo inverted />
          <span className="rounded-md bg-turmeric px-1.5 py-0.5 text-[0.65rem] font-bold uppercase tracking-wider text-night [:lang(bn)_&]:tracking-normal">
            {t('admin.title')}
          </span>
        </Link>
        {onClose && (
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="rounded-lg p-1.5 text-white/70 transition hover:bg-white/10 hover:text-white"
          >
            <X aria-hidden="true" className="h-5 w-5" />
          </button>
        )}
      </div>

      <nav
        aria-label={t('admin.nav.label')}
        className="flex-1 overflow-y-auto px-3 py-5 [scrollbar-color:rgb(255_255_255/0.15)_transparent] [scrollbar-width:thin]"
      >
        {visible.map((group) => (
          <div key={group.key} className="mb-6 last:mb-0">
            <p className="mb-2 px-3 text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-white/40 [:lang(bn)_&]:tracking-normal">
              {t(`admin.groups.${group.key}`)}
            </p>
            <ul className="flex flex-col gap-0.5">
              {group.sections.map(({ to, label, icon: Icon, count, urgent }) => {
                const value = count && counts.data ? Number(counts.data[count]) : 0;
                return (
                  <li key={to}>
                    <NavLink
                      to={to}
                      end={to === '/admin'}
                      className={({ isActive }) =>
                        `group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition ${
                          isActive
                            ? 'bg-white/10 text-white shadow-[inset_3px_0_0_var(--color-turmeric)]'
                            : 'text-white/70 hover:bg-white/5 hover:text-white'
                        }`
                      }
                    >
                      <Icon aria-hidden="true" className="h-4.5 w-4.5 shrink-0" />
                      <span className="flex-1 truncate">{t(label)}</span>
                      {value > 0 && (
                        <span
                          className={`min-w-6 rounded-full px-1.5 py-0.5 text-center text-xs font-bold ${
                            urgent
                              ? 'animate-pulse bg-jamdani text-white'
                              : 'bg-turmeric text-night'
                          }`}
                        >
                          {formatCount(value, language)}
                          <span className="sr-only"> {t('admin.portal.waiting')}</span>
                        </span>
                      )}
                    </NavLink>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="shrink-0 border-t border-white/10 p-4">
        <div className="flex items-center gap-3 rounded-xl bg-white/5 p-3">
          <span
            aria-hidden="true"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-hill font-display text-sm font-bold"
          >
            {(profile?.displayName ?? '?').charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold">
              {profile?.displayName ?? t('account.title')}
            </p>
            <p className="truncate text-xs text-dusk">
              {staffRoles.map((role) => t(`roles.${role}`)).join(' · ')}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

/** Below `lg`: the sidebar in a panel from the left, on the native dialog (focus is trapped). */
function MobileDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    // jsdom has no showModal; fall back to the open attribute there.
    if (open && !dialog.open) {
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    } else if (!open && dialog.open) {
      if (typeof dialog.close === 'function') dialog.close();
      else dialog.removeAttribute('open');
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={onClose}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
      className="m-0 h-dvh max-h-dvh w-[min(18rem,85vw)] max-w-none bg-night p-0 backdrop:bg-night/60 backdrop:backdrop-blur-sm lg:hidden"
    >
      <h2 id={titleId} className="sr-only">
        {t('admin.nav.label')}
      </h2>
      {open && <Sidebar onClose={onClose} />}
    </dialog>
  );
}

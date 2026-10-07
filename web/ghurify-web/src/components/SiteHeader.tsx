import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, NavLink, useLocation } from 'react-router';
import {
  ChevronDown,
  Compass,
  LayoutDashboard,
  LogIn,
  Map,
  Menu,
  ShieldCheck,
  Tent,
  User,
  X,
} from 'lucide-react';
import { useAuthStore } from '@/features/auth/authStore';
import { isStaff } from '@/features/auth/profileApi';
import { useMyProfile } from '@/features/auth/useProfile';
import { useTourStore } from '@/components/tour/tourStore';
import { useIsOverHero } from './ui/headerStore';
import { LanguageToggle } from './LanguageToggle';
import { NotificationBell } from './NotificationBell';
import { Logo } from './Logo';

/** The public sections, in the order they appear everywhere (bar, drawer, footer). */
function usePublicLinks() {
  const { t } = useTranslation();

  return [
    { to: '/trips', label: t('nav.explore'), end: false },
    { to: '/feed', label: t('nav.stories'), end: false },
    { to: '/#destinations', label: t('nav.destinations'), end: true },
    { to: '/#safety', label: t('nav.safety'), end: true },
  ];
}

/**
 * Signed-in destinations, by role: shown for convenience only, every screen behind them is
 * protected by the API regardless.
 */
function useAccountLinks() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const { data: profile } = useMyProfile();

  if (status !== 'authenticated') return [];

  return [
    { to: '/account', label: t('account.title'), icon: User },
    { to: '/me/trips', label: t('nav.myTrips'), icon: Map },
    ...(profile?.roles.includes('Host')
      ? [{ to: '/host/trips', label: t('nav.hosting'), icon: Tent }]
      : []),
    ...(isStaff(profile?.roles)
      ? [{ to: '/admin', label: t('nav.admin'), icon: LayoutDashboard }]
      : []),
  ];
}

/** True once the page has scrolled past the top, when the header turns solid. */
function useScrolled(threshold = 24) {
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const update = () => setScrolled(window.scrollY > threshold);
    update();
    window.addEventListener('scroll', update, { passive: true });
    return () => window.removeEventListener('scroll', update);
  }, [threshold]);

  return scrolled;
}

/**
 * Fixed top bar. Over a page that opens on a full-bleed photo it starts transparent with
 * white text and turns solid white once the page scrolls; everywhere else it is solid from
 * the start, with a spacer so content is not hidden under it. Below `lg` the sections move
 * into a drawer.
 */
export function SiteHeader() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const startTour = useTourStore((state) => state.start);
  const overHero = useIsOverHero();
  const scrolled = useScrolled();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const links = usePublicLinks();
  const closeDrawer = useCallback(() => setDrawerOpen(false), []);

  // Any navigation closes the drawer (reset while rendering, not in an effect).
  const { key: locationKey } = useLocation();
  const [seenLocation, setSeenLocation] = useState(locationKey);
  if (seenLocation !== locationKey) {
    setSeenLocation(locationKey);
    setDrawerOpen(false);
  }
  const glass = overHero && !scrolled && !drawerOpen;

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `relative py-2 text-[0.95rem] font-medium transition after:absolute after:inset-x-0 after:-bottom-0.5 after:h-0.5 after:origin-left after:rounded-full after:transition-transform ${
      glass
        ? `text-white hover:text-dusk after:bg-dusk ${isActive ? 'text-dusk' : ''}`
        : `text-deep hover:text-hill after:bg-turmeric ${isActive ? 'text-hill' : ''}`
    } ${isActive ? 'after:scale-x-100' : 'after:scale-x-0 hover:after:scale-x-100'}`;

  return (
    <>
      <header
        className={`fixed inset-x-0 top-0 z-40 transition-[background-color,box-shadow,color] duration-300 ${
          glass
            ? 'bg-linear-to-b from-night/55 to-transparent text-white'
            : 'bg-white/95 text-deep shadow-[0_5px_20px_rgba(15,42,31,0.08)] backdrop-blur'
        }`}
      >
        <div className="container-page flex h-18 items-center justify-between gap-4">
          <Link to="/" aria-label={t('nav.home')} className="shrink-0 rounded-xl">
            <Logo inverted={glass} />
          </Link>

          <nav aria-label={t('nav.main')} className="hidden lg:block">
            <ul className="flex items-center gap-7">
              {links.map((link) => (
                <li key={link.to}>
                  {link.to.includes('#') ? (
                    <Link to={link.to} className={linkClass({ isActive: false })}>
                      {link.label}
                    </Link>
                  ) : (
                    <NavLink to={link.to} end={link.end} className={linkClass}>
                      {link.label}
                    </NavLink>
                  )}
                </li>
              ))}
            </ul>
          </nav>

          <div className="flex items-center gap-1.5 sm:gap-2">
            <button
              type="button"
              onClick={startTour}
              className={`hidden items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium transition xl:inline-flex ${
                glass ? 'text-white hover:bg-white/15' : 'text-deep hover:bg-hill/10'
              }`}
            >
              <Compass aria-hidden="true" className="h-4 w-4" />
              {t('nav.tour')}
            </button>

            <div data-tour="language">
              <LanguageToggle compact />
            </div>

            <div data-tour="account" className="flex items-center gap-1">
              {/* While the silent refresh is still deciding, show neither: offering "sign in"
                  to someone who turns out to be signed in is worse than a brief gap. */}
              {status === 'authenticated' ? (
                <>
                  <NotificationBell />
                  <AccountMenu glass={glass} />
                </>
              ) : status === 'anonymous' ? (
                <Link
                  to="/login"
                  className="inline-flex items-center gap-2 rounded-full bg-turmeric px-3 py-2 text-sm font-semibold text-deep shadow-sm transition hover:bg-dusk sm:px-5"
                >
                  <LogIn aria-hidden="true" className="h-4 w-4" />
                  <span className="hidden sm:inline">{t('auth.signIn')}</span>
                  <span className="sr-only sm:hidden">{t('auth.signIn')}</span>
                </Link>
              ) : (
                <span className="inline-block h-9 w-10 sm:w-24" aria-hidden="true" />
              )}
            </div>

            <button
              type="button"
              onClick={() => setDrawerOpen(true)}
              aria-label={t('nav.openMenu')}
              aria-haspopup="dialog"
              className={`rounded-full p-2 transition lg:hidden ${
                glass ? 'text-white hover:bg-white/15' : 'text-deep hover:bg-hill/10'
              }`}
            >
              <Menu aria-hidden="true" className="h-6 w-6" />
            </button>
          </div>
        </div>
      </header>

      {/* Holds the header's place on pages without a photo under it. */}
      {!overHero && <div aria-hidden="true" className="h-18" />}

      <MobileDrawer open={drawerOpen} onClose={closeDrawer} />
    </>
  );
}

/** The signed-in menu: an initial in a circle that opens the account destinations. */
export function AccountMenu({ glass }: { glass: boolean }) {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const links = useAccountLinks();
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const { key: locationKey } = useLocation();
  const [seenLocation, setSeenLocation] = useState(locationKey);
  const name = user?.displayName ?? t('account.title');

  // Close on navigation (reset while rendering), outside click and Escape.
  if (seenLocation !== locationKey) {
    setSeenLocation(locationKey);
    setOpen(false);
  }

  useEffect(() => {
    if (!open) return;

    function onPointer(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') setOpen(false);
    }

    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-controls={menuId}
        className={`flex items-center gap-1.5 rounded-full p-1 pr-2 text-sm font-medium transition ${
          glass ? 'text-white hover:bg-white/15' : 'text-deep hover:bg-hill/10'
        }`}
      >
        <span
          aria-hidden="true"
          className="flex h-8 w-8 items-center justify-center rounded-full bg-hill font-display text-sm font-bold text-white ring-2 ring-white/70"
        >
          {name.charAt(0).toUpperCase()}
        </span>
        <span className="sr-only">{t('nav.accountMenu')}</span>
        <ChevronDown
          aria-hidden="true"
          className={`hidden h-4 w-4 transition-transform sm:block ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <div
          id={menuId}
          className="absolute right-0 z-50 mt-3 w-60 animate-fade-in overflow-hidden rounded-2xl bg-white text-deep shadow-xl ring-1 ring-hill/10"
        >
          <p className="truncate border-b border-hill/10 px-4 py-3 text-sm">
            <span className="block text-xs text-deep/60">{t('nav.signedInAs')}</span>
            <span className="font-semibold">{name}</span>
          </p>
          <ul className="py-1.5">
            {links.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-4 py-2.5 text-sm transition hover:bg-mist ${
                      isActive ? 'font-semibold text-hill' : ''
                    }`
                  }
                >
                  <Icon aria-hidden="true" className="h-4 w-4 text-hill" />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/**
 * Below `lg`: the sections in a panel from the right, on the native &lt;dialog&gt; so focus is
 * trapped and Escape closes it. Closes itself on navigation.
 */
function MobileDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const links = usePublicLinks();
  const accountLinks = useAccountLinks();
  const startTour = useTourStore((state) => state.start);
  const status = useAuthStore((state) => state.status);

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

  const itemClass =
    'flex items-center justify-between rounded-xl px-4 py-3 font-display text-lg font-semibold text-deep transition hover:bg-mist';

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={onClose}
      onClick={(event) => {
        // A click on the backdrop lands on the dialog element itself.
        if (event.target === event.currentTarget) onClose();
      }}
      className="m-0 ml-auto h-dvh max-h-dvh w-[min(22rem,88vw)] max-w-none bg-white p-0 shadow-2xl backdrop:bg-night/60 backdrop:backdrop-blur-sm"
    >
      {open && (
        <div className="flex h-full animate-fade-in flex-col">
          <div className="flex h-18 items-center justify-between border-b border-hill/10 px-5">
            <h2 id={titleId} className="sr-only">
              {t('nav.menu')}
            </h2>
            <Logo />
            <button
              type="button"
              onClick={onClose}
              aria-label={t('common.close')}
              className="rounded-full p-2 text-deep transition hover:bg-hill/10"
            >
              <X aria-hidden="true" className="h-6 w-6" />
            </button>
          </div>

          <nav aria-label={t('nav.main')} className="flex-1 overflow-y-auto px-3 py-4">
            <ul className="flex flex-col gap-1">
              {links.map((link) => (
                <li key={link.to}>
                  <Link to={link.to} className={itemClass}>
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>

            {accountLinks.length > 0 && (
              <>
                <p className="eyebrow mt-6 px-4">{t('nav.yourAccount')}</p>
                <ul className="mt-2 flex flex-col gap-1">
                  {accountLinks.map(({ to, label, icon: Icon }) => (
                    <li key={to}>
                      <Link to={to} className={`${itemClass} justify-start gap-3`}>
                        <Icon aria-hidden="true" className="h-5 w-5 text-hill" />
                        {label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </nav>

          <div className="flex flex-col gap-3 border-t border-hill/10 p-5">
            <button
              type="button"
              onClick={() => {
                onClose();
                startTour();
              }}
              className="inline-flex items-center justify-center gap-2 rounded-full border border-hill/20 px-4 py-2.5 text-sm font-medium text-deep transition hover:bg-mist"
            >
              <Compass aria-hidden="true" className="h-4 w-4" />
              {t('nav.tour')}
            </button>
            {status === 'anonymous' && (
              <Link
                to="/login"
                className="inline-flex items-center justify-center gap-2 rounded-full bg-turmeric px-4 py-3 font-semibold text-deep transition hover:bg-dusk"
              >
                <LogIn aria-hidden="true" className="h-4 w-4" />
                {t('auth.signIn')}
              </Link>
            )}
            <p className="flex items-center justify-center gap-1.5 text-xs text-deep/60">
              <ShieldCheck aria-hidden="true" className="h-4 w-4 text-hill" />
              {t('app.tagline')}
            </p>
          </div>
        </div>
      )}
    </dialog>
  );
}

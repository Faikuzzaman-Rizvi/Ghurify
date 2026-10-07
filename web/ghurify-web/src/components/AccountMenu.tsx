import { useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, useLocation } from 'react-router';
import { ChevronDown, LogOut } from 'lucide-react';
import { Avatar } from '@/components/Avatar';
import { useAccountLinks, useSignedInIdentity } from '@/features/auth/useAccount';
import { useLogout } from '@/features/auth/useAuthMutations';

/**
 * The signed-in menu, in the site header and the admin portal's top bar: the profile picture
 * opens who is signed in, their account destinations, and signing out. Closes on navigation, an
 * outside click and Escape.
 */
export function AccountMenu({ glass }: { glass: boolean }) {
  const { t } = useTranslation();
  const identity = useSignedInIdentity();
  const links = useAccountLinks();
  const logout = useLogout();
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const { key: locationKey } = useLocation();
  const [seenLocation, setSeenLocation] = useState(locationKey);

  // Close on navigation (reset while rendering, not in an effect).
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
        <Avatar
          userId={identity.userId}
          name={identity.name}
          version={identity.avatarVersion}
          size="sm"
          className="ring-2 ring-white/70"
        />
        <span className="sr-only">{t('nav.accountMenu')}</span>
        <ChevronDown
          aria-hidden="true"
          className={`hidden h-4 w-4 transition-transform sm:block ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <div
          id={menuId}
          className="absolute right-0 z-50 mt-3 w-64 animate-menu-in overflow-hidden rounded-2xl bg-white text-deep shadow-xl ring-1 ring-hill/10"
        >
          <div className="flex items-center gap-3 border-b border-hill/10 px-4 py-3">
            <Avatar
              userId={identity.userId}
              name={identity.name}
              version={identity.avatarVersion}
              size="md"
            />
            <p className="min-w-0 text-sm">
              <span className="block text-xs text-deep/60">{t('nav.signedInAs')}</span>
              <span className="block truncate font-semibold">{identity.name}</span>
              {identity.email && (
                <span className="block truncate text-xs text-deep/60">{identity.email}</span>
              )}
            </p>
          </div>

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

          <div className="border-t border-hill/10 py-1.5">
            <button
              type="button"
              onClick={() => logout.mutate()}
              disabled={logout.isPending}
              className="flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm font-medium text-jamdani transition hover:bg-mist disabled:opacity-60"
            >
              <LogOut aria-hidden="true" className="h-4 w-4" />
              {logout.isPending ? t('nav.signingOut') : t('auth.signOut')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

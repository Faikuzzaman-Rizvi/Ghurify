import { useTranslation } from 'react-i18next';
import { LayoutDashboard, Map, MapPinned, ReceiptText, Tent, User } from 'lucide-react';
import { useAuthStore } from './authStore';
import { isStaff } from './profileApi';
import { useMyProfile } from './useProfile';

/**
 * Signed-in destinations, by role: shown for convenience only, every screen behind them is
 * protected by the API regardless.
 */
export function useAccountLinks() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const { data: profile } = useMyProfile();

  if (status !== 'authenticated') return [];

  return [
    { to: '/account', label: t('account.title'), icon: User },
    { to: '/me/trips', label: t('nav.myTrips'), icon: Map },
    { to: '/me/map', label: t('nav.travelMap'), icon: MapPinned },
    { to: '/me/payments', label: t('nav.payments'), icon: ReceiptText },
    ...(profile?.roles.includes('Host')
      ? [{ to: '/host/trips', label: t('nav.hosting'), icon: Tent }]
      : []),
    ...(isStaff(profile?.roles)
      ? [{ to: '/admin', label: t('nav.admin'), icon: LayoutDashboard }]
      : []),
  ];
}

/**
 * The signed-in person's picture (their initial until they add one) and name, as the profile
 * knows them. The picture's version comes with the profile: until it has loaded, and when there
 * is no picture, the initial shows and nothing is requested.
 */
export function useSignedInIdentity() {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const { data: profile } = useMyProfile();

  return {
    userId: user?.id ?? 0,
    name: profile?.displayName ?? user?.displayName ?? t('account.title'),
    email: profile?.maskedEmail ?? user?.maskedEmail ?? '',
    avatarVersion: profile ? (profile.avatarVersion ?? null) : null,
  };
}

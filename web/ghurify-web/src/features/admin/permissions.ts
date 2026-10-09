import { useMyProfile } from '@/features/auth/useProfile';
import type { Profile } from '@/features/auth/profileApi';

/**
 * The permission keys, mirroring Ghurify.Domain.Identity.Permissions.
 *
 * These decide what the portal shows: which sections appear in the sidebar, which routes open,
 * which buttons are rendered. They are never the security boundary — the API checks the same
 * permission on every call, reading it from the database rather than from anything the browser
 * sends — so a key that drifts out of step with the backend hides a section rather than opening
 * one. The role editor does not use this list at all: it renders the catalogue the API returns,
 * so a permission added server-side appears there without a frontend release.
 */
export const permissions = {
  dashboardView: 'dashboard.view',

  usersView: 'users.view',
  usersSuspend: 'users.suspend',
  usersSecurity: 'users.security',
  usersRolesManage: 'users.roles.manage',
  usersVerify: 'users.verify',
  usersDocumentsView: 'users.documents.view',
  usersAvatarRemove: 'users.avatar.remove',

  tripsView: 'trips.view',
  tripsCancel: 'trips.cancel',
  destinationsManage: 'destinations.manage',

  bookingsView: 'bookings.view',
  paymentsView: 'payments.view',
  paymentsRefundRetry: 'payments.refund.retry',
  payoutsView: 'payouts.view',
  payoutsApprove: 'payouts.approve',

  safetySosView: 'safety.sos.view',
  safetySosManage: 'safety.sos.manage',
  safetyCheckInsView: 'safety.checkins.view',
  safetyDestinationsStatus: 'safety.destinations.status',
  safetyPointsManage: 'safety.points.manage',

  moderationReportsView: 'moderation.reports.view',
  moderationReportsResolve: 'moderation.reports.resolve',
  moderationContentManage: 'moderation.content.manage',
  moderationDisputes: 'moderation.disputes',

  settingsBranding: 'settings.branding',
  settingsTheme: 'settings.theme',

  staffView: 'staff.view',
  staffAssign: 'staff.assign',
  staffRolesManage: 'staff.roles.manage',

  auditView: 'audit.view',
} as const;

export type PermissionKey = (typeof permissions)[keyof typeof permissions];

/** Whether a profile holds one permission. A super admin holds every one of them. */
export function can(
  profile: Pick<Profile, 'permissions' | 'isSuperAdmin'> | undefined,
  permission: string,
): boolean {
  return profile?.isSuperAdmin === true || (profile?.permissions ?? []).includes(permission);
}

/** Whether a profile holds at least one of several permissions. */
export function canAny(
  profile: Pick<Profile, 'permissions' | 'isSuperAdmin'> | undefined,
  wanted: readonly string[],
): boolean {
  return wanted.some((permission) => can(profile, permission));
}

/**
 * What the signed-in person may do on the admin desk, for hiding sections and buttons.
 * `ready` is false while the profile is still loading, so a screen can wait rather than flash
 * an empty state at somebody who does in fact have access.
 */
export function usePermissions() {
  const { data: profile, isPending } = useMyProfile();

  return {
    ready: !isPending,
    isSuperAdmin: profile?.isSuperAdmin === true,
    can: (permission: string) => can(profile, permission),
    canAny: (wanted: readonly string[]) => canAny(profile, wanted),
  };
}

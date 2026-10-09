import type { TFunction } from 'i18next';
import type { UserStatus } from './adminApi';

/** Badge colours for account statuses, shared by the user list and the user page. */
export const statusBadge: Record<UserStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-800',
  Suspended: 'bg-jamdani/10 text-jamdani',
  Deactivated: 'bg-mist text-deep/70',
  PendingEmail: 'bg-turmeric/20 text-deep',
};

/**
 * The reader's own words for a permission key (`payouts.approve`) and for the group it belongs
 * to. Kept here rather than in the i18n files' shape so an unknown key — one a newer backend
 * defines and this build has no label for — degrades to the key itself instead of to a missing
 * translation. The role editor stays usable either way.
 */
export function permissionLabel(permission: string, t: TFunction): string {
  const key = `admin.permissions.${permission}`;
  const translated = t(key);
  return translated === key ? permission : translated;
}

export function permissionGroupLabel(group: string, t: TFunction): string {
  const key = `admin.permissionGroups.${group}`;
  const translated = t(key);
  return translated === key ? group : translated;
}

import type { UserStatus } from './adminApi';

/** Badge colours for account statuses, shared by the user list and the user page. */
export const statusBadge: Record<UserStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-800',
  Suspended: 'bg-jamdani/10 text-jamdani',
  Deactivated: 'bg-mist text-deep/70',
  PendingEmail: 'bg-turmeric/20 text-deep',
};

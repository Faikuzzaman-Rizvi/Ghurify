import type { ReactElement } from 'react';
import { RoleRoute } from '@/features/auth/RoleRoute';
import { can } from './permissions';

/**
 * Gates an admin route on one permission.
 *
 * Like every other guard in the app it only decides what is worth rendering: the API checks the
 * same permission on each call it serves, against the database rather than anything the browser
 * sends. Someone who reaches the address without the permission sees the "not allowed" screen
 * instead of a page whose every request would fail.
 */
export function PermissionRoute({
  needs,
  children,
}: {
  needs: string;
  children: ReactElement;
}) {
  return <RoleRoute allow={(profile) => can(profile, needs)}>{children}</RoleRoute>;
}

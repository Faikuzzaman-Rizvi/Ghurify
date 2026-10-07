import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { EmptyState, ErrorState } from '@/components/States';
import { ProtectedRoute } from './ProtectedRoute';
import type { Profile } from './profileApi';
import { useMyProfile } from './useProfile';

/**
 * Gates a route on the signed-in user's roles or verification. Like ProtectedRoute it only
 * hides screens; the API enforces every rule itself.
 */
export function RoleRoute({
  allow,
  children,
}: {
  allow: (profile: Profile) => boolean;
  children: ReactElement;
}) {
  return (
    <ProtectedRoute>
      <RoleGate allow={allow}>{children}</RoleGate>
    </ProtectedRoute>
  );
}

function RoleGate({
  allow,
  children,
}: {
  allow: (profile: Profile) => boolean;
  children: ReactElement;
}) {
  const { t } = useTranslation();
  const { data: profile, isPending, isError, refetch } = useMyProfile();

  if (isPending) {
    return (
      <p role="status" className="p-6 text-deep/70">
        {t('common.loading')}
      </p>
    );
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <ErrorState message={t('common.error')} onRetry={() => void refetch()} />
      </div>
    );
  }

  if (!allow(profile)) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <EmptyState title={t('errors.forbidden')} hint={t('errors.forbiddenHint')} />
      </div>
    );
  }

  return children;
}

import type { ReactElement } from 'react';
import { Navigate, useLocation } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from './authStore';

/**
 * Gates a route on being signed in.
 *
 * This is a convenience, not a security control: it hides screens the user cannot use. Every
 * endpoint behind it enforces its own authorization, because anything decided in the browser
 * can be bypassed in the browser.
 */
export function ProtectedRoute({ children }: { children: ReactElement }) {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const location = useLocation();

  // The silent refresh has not answered yet. Showing the login page here would flash it at
  // someone who turns out to be signed in.
  if (status === 'unknown') {
    return (
      <p role="status" className="p-6 text-deep/70">
        {t('common.loading')}
      </p>
    );
  }

  if (status === 'anonymous') {
    // Remember where they were headed so sign-in can return them there.
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return children;
}

import { useEffect, useRef } from 'react';
import { asNumber } from '@/api/client';
import { authApi } from './authApi';
import { useAuthStore } from './authStore';

/**
 * Access tokens last 15 minutes. Renewing a minute early means a request is never sent with a
 * token that expires in flight.
 */
const RENEW_MARGIN_SECONDS = 60;

/**
 * Keeps the session alive.
 *
 * On mount it asks the API to refresh. That call carries the httpOnly cookie, so it succeeds
 * for someone who is still signed in and fails harmlessly for everyone else — which is how a
 * reload restores the session without any token being readable by page scripts.
 */
export function useSilentRefresh(): void {
  const signIn = useAuthStore((state) => state.signIn);
  const markAnonymous = useAuthStore((state) => state.markAnonymous);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    let cancelled = false;

    async function refresh(): Promise<void> {
      try {
        const session = await authApi.refresh();

        if (cancelled) {
          return;
        }

        signIn(session);

        const renewInMs =
          Math.max(
            asNumber(session.expiresInSeconds) - RENEW_MARGIN_SECONDS,
            RENEW_MARGIN_SECONDS,
          ) * 1000;

        timer.current = setTimeout(() => void refresh(), renewInMs);
      } catch {
        // No cookie, or it has expired or been revoked. Not an error worth showing: it is
        // simply what being signed out looks like.
        if (!cancelled) {
          markAnonymous();
        }
      }
    }

    void refresh();

    return () => {
      cancelled = true;
      if (timer.current) {
        clearTimeout(timer.current);
      }
    };
  }, [signIn, markAnonymous]);
}

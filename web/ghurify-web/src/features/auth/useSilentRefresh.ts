import { useEffect } from 'react';
import { ApiError, setSessionRenewer } from '@/api/client';
import { refreshSession } from './authApi';
import { useAuthStore } from './authStore';

/**
 * Access tokens last 15 minutes. Renewing a minute early means a request is never sent with a
 * token that expires in flight.
 */
const RENEW_MARGIN_MS = 60_000;

/** Waits between attempts when the API is unreachable, busy or failing. */
const RETRY_DELAYS_MS = [2_000, 5_000, 15_000];

/**
 * Only an answer from the API that the session is over (401, or 400/403 for a cookie it refuses)
 * means signed out. No connection, a rate limit (429) or a server error (5xx) says nothing about
 * the session, and treating them as "signed out" threw people out while they clicked around.
 */
function isTemporary(error: unknown): boolean {
  return !(error instanceof ApiError) || error.status === 429 || error.status >= 500;
}

/**
 * Keeps the session alive.
 *
 * On mount it asks the API to refresh. That call carries the httpOnly cookie, so it succeeds
 * for someone who is still signed in and fails harmlessly for everyone else — which is how a
 * reload restores the session without any token being readable by page scripts.
 *
 * After that, whenever there is a session (restored, or from signing in, or a new password), it is
 * renewed a minute before the access token expires; and a request that still finds its token
 * expired renews it once and tries again (see setSessionRenewer).
 */
export function useSilentRefresh(): void {
  const signIn = useAuthStore((state) => state.signIn);
  const markAnonymous = useAuthStore((state) => state.markAnonymous);
  const expiresAt = useAuthStore((state) => state.expiresAt);

  // Restore on load, retrying while the API cannot answer; until then the status stays
  // "unknown", which screens show as loading rather than as signed out.
  useEffect(() => {
    let cancelled = false;
    let retry: ReturnType<typeof setTimeout> | undefined;

    async function restore(attempt: number): Promise<void> {
      try {
        const session = await refreshSession();
        if (cancelled) return;
        // None: nobody is signed in on this browser.
        if (session) signIn(session);
        else markAnonymous();
      } catch (error) {
        if (cancelled) return;
        const delay = RETRY_DELAYS_MS[attempt];
        if (isTemporary(error) && delay !== undefined) {
          retry = setTimeout(() => void restore(attempt + 1), delay);
        } else {
          // The cookie has expired or been revoked. Not an error worth showing: it is simply
          // what being signed out looks like.
          markAnonymous();
        }
      }
    }

    void restore(0);

    return () => {
      cancelled = true;
      clearTimeout(retry);
    };
  }, [signIn, markAnonymous]);

  // Renew shortly before the token expires. signIn sets a new expiry, which schedules the next.
  useEffect(() => {
    if (expiresAt === null) return;

    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function renew(attempt: number): Promise<void> {
      try {
        const session = await refreshSession();
        if (cancelled) return;
        // None: nobody is signed in on this browser.
        if (session) signIn(session);
        else markAnonymous();
      } catch (error) {
        if (cancelled) return;
        const delay = RETRY_DELAYS_MS[attempt];
        if (isTemporary(error) && delay !== undefined) {
          timer = setTimeout(() => void renew(attempt + 1), delay);
        } else if (!isTemporary(error)) {
          // Ended elsewhere: signed out on another device, a new password, a suspended account.
          markAnonymous();
        }
        // Still unreachable after every retry: keep the session. A request that then finds its
        // token expired renews it itself.
      }
    }

    timer = setTimeout(
      () => void renew(0),
      Math.max(expiresAt - Date.now() - RENEW_MARGIN_MS, 1_000),
    );

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [expiresAt, signIn, markAnonymous]);

  // For requests that find their token expired anyway (a laptop that slept through the renewal).
  useEffect(() => {
    setSessionRenewer(async () => {
      try {
        const session = await refreshSession();
        if (session) signIn(session);
        else markAnonymous();
        return session !== null;
      } catch (error) {
        if (!isTemporary(error)) markAnonymous();
        return false;
      }
    });

    return () => setSessionRenewer(null);
  }, [signIn, markAnonymous]);
}

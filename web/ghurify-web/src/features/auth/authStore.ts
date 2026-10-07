import { create } from 'zustand';
import { setAccessToken, asNumber } from '@/api/client';
import type { SessionResponse } from './authApi';

export interface AuthUser {
  id: number;
  maskedEmail: string;
  displayName: string | null;
}

/**
 * `unknown` is the state before the silent refresh has answered. Screens must treat it as
 * "still deciding" rather than "signed out", or a reload would flash the login page at
 * someone who is actually signed in.
 */
export type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

interface AuthState {
  status: AuthStatus;
  user: AuthUser | null;
  /**
   * When the current access token stops working (epoch milliseconds), or null when signed out.
   * The session is renewed shortly before, however it began: restored on load, signed in, or a
   * new password.
   */
  expiresAt: number | null;
  signIn: (session: SessionResponse) => void;
  signOut: () => void;
  markAnonymous: () => void;
}

/**
 * Not persisted, on purpose. The access token lives in a module variable in the API client
 * and the session is restored from the httpOnly refresh cookie, so nothing a page script can
 * read survives a reload.
 */
export const useAuthStore = create<AuthState>()((set) => ({
  status: 'unknown',
  user: null,
  expiresAt: null,

  signIn: (session) => {
    setAccessToken(session.accessToken);
    set({
      status: 'authenticated',
      user: {
        id: asNumber(session.user.id),
        maskedEmail: session.user.maskedEmail,
        displayName: session.user.displayName ?? null,
      },
      expiresAt: Date.now() + asNumber(session.expiresInSeconds) * 1000,
    });
  },

  signOut: () => {
    setAccessToken(null);
    set({ status: 'anonymous', user: null, expiresAt: null });
  },

  markAnonymous: () => {
    setAccessToken(null);
    set({ status: 'anonymous', user: null, expiresAt: null });
  },
}));

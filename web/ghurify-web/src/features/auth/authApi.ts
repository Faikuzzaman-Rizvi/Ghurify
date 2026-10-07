import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type SessionResponse = components['schemas']['SessionResponse'];
export type CodeSentResponse = components['schemas']['CodeSentResponse'];
export type WhoAmIResponse = components['schemas']['WhoAmIResponse'];
export type RegisterCommand = components['schemas']['RegisterCommand'];
export type ResetPasswordCommand = components['schemas']['ResetPasswordCommand'];
export type ChangePasswordCommand = components['schemas']['ChangePasswordCommand'];

const anonymous = { authenticated: false } as const;

/**
 * The account endpoints. Sign-in is email and password; a code is only emailed to confirm a new
 * account's address, and to reset a forgotten password. The refresh token is never handled
 * here: it travels in an httpOnly cookie the browser attaches and page scripts cannot read.
 */
export const authApi = {
  register: (command: RegisterCommand) =>
    apiPost<CodeSentResponse>('/api/v1/auth/register', command, anonymous),

  confirmEmail: (email: string, code: string) =>
    apiPost<SessionResponse>('/api/v1/auth/register/confirm', { email, code }, anonymous),

  resendCode: (email: string) =>
    apiPost<CodeSentResponse>('/api/v1/auth/register/resend', { email }, anonymous),

  signIn: (email: string, password: string) =>
    apiPost<SessionResponse>('/api/v1/auth/sign-in', { email, password }, anonymous),

  forgotPassword: (email: string) =>
    apiPost<CodeSentResponse>('/api/v1/auth/password/forgot', { email }, anonymous),

  resetPassword: (command: ResetPasswordCommand) =>
    apiPost<SessionResponse>('/api/v1/auth/password/reset', command, anonymous),

  changePassword: (command: ChangePasswordCommand) =>
    apiPost<SessionResponse>('/api/v1/auth/password/change', command),

  /** A new session from the refresh cookie; undefined (204) when this browser has no cookie. */
  refresh: () => apiPost<SessionResponse | undefined>('/api/v1/auth/refresh', undefined, anonymous),

  logout: () => apiPost<void>('/api/v1/auth/logout', undefined, anonymous),

  whoami: () => apiGet<WhoAmIResponse>('/api/v1/auth/whoami'),
};

let refreshing: Promise<SessionResponse | null> | null = null;

/**
 * Renews the session, at most once at a time; null when nobody is signed in on this browser.
 * Refresh tokens rotate, and the API treats a token presented twice as stolen and ends the whole
 * session; so two refreshes must never race with the same cookie. Within a tab, callers share one
 * request (React StrictMode mounts effects twice in development); across tabs, a Web Lock makes
 * them take turns, and each one sends the cookie the previous one left.
 */
export function refreshSession(): Promise<SessionResponse | null> {
  refreshing ??= withRefreshLock(async () => (await authApi.refresh()) ?? null).finally(() => {
    refreshing = null;
  });
  return refreshing;
}

async function withRefreshLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = typeof navigator === 'undefined' ? undefined : navigator.locks;
  // await unwraps the promise the lock callback returns.
  return locks ? await locks.request('ghurify.session-refresh', work) : await work();
}

import { apiPost, apiGet } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type SessionResponse = components['schemas']['SessionResponse'];
export type RequestOtpResponse = components['schemas']['RequestOtpResponse'];
export type WhoAmIResponse = components['schemas']['WhoAmIResponse'];

/**
 * The sign-in endpoints. None of them send a bearer token: that is the point of them.
 * The refresh token is never handled here — it travels in an httpOnly cookie the browser
 * attaches and page scripts cannot read.
 */
export const authApi = {
  requestOtp: (email: string) =>
    apiPost<RequestOtpResponse>('/api/v1/auth/otp', { email }, { authenticated: false }),

  verifyOtp: (email: string, code: string) =>
    apiPost<SessionResponse>('/api/v1/auth/verify', { email, code }, { authenticated: false }),

  refresh: () =>
    apiPost<SessionResponse>('/api/v1/auth/refresh', undefined, { authenticated: false }),

  logout: () => apiPost<void>('/api/v1/auth/logout', undefined, { authenticated: false }),

  whoami: () => apiGet<WhoAmIResponse>('/api/v1/auth/whoami'),
};

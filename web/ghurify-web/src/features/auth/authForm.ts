import { useEffect, useState } from 'react';
import type { TFunction } from 'i18next';
import { useLocation } from 'react-router';
import { z } from 'zod';

import { ApiError } from '@/api/client';
import { errorText } from '@/lib/errors';

/** The input look shared by every account screen. */
export const authInputClass =
  'w-full rounded-xl border border-hill/15 bg-mist/60 px-4 py-3.5 text-base text-deep transition placeholder:text-deep/35 hover:border-hill/30 focus:border-hill focus:bg-white focus:outline-none focus:ring-4 focus:ring-hill/10 aria-invalid:border-jamdani aria-invalid:ring-jamdani/10';

export const authButtonClass =
  'inline-flex items-center justify-center gap-2 rounded-full bg-linear-to-r from-hill to-deep px-4 py-3.5 font-semibold text-white shadow-[0_10px_24px_rgba(36,92,67,0.35)] transition hover:-translate-y-0.5 hover:shadow-[0_14px_30px_rgba(36,92,67,0.45)] focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-turmeric/50 disabled:translate-y-0 disabled:opacity-60';

/**
 * Mirrors the backend: one @, something before it, a dotted domain after it. Telling the person
 * about a bad address before a request is made saves a round trip.
 */
export const emailField = z
  .string()
  .trim()
  .max(254)
  .refine((value) => /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/.test(value), { message: 'emailInvalid' });

/** The length rule the API enforces. Commonness is checked by the API, which has the list. */
export const newPasswordField = z
  .string()
  .min(10, { message: 'passwordTooShort' })
  .max(128, { message: 'passwordTooLong' });

export const codeField = z
  .string()
  .trim()
  .regex(/^\d{6}$/, { message: 'codeInvalid' });

/** Where to go after signing in: back to the page that asked for it, or home. */
export function useReturnTo(): string {
  const location = useLocation();
  const from = (location.state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/';
}

/** An account error in the reader's language; a pause says how long. */
export function authErrorText(error: unknown, t: TFunction): string {
  if (error instanceof ApiError && error.code === 'sign_in_paused') {
    return t('errors.sign_in_paused', {
      minutes: Math.max(1, Math.ceil((error.retryAfterSeconds ?? 900) / 60)),
    });
  }

  return errorText(error, t);
}

/** Counts a "send again" button back in, one second at a time. */
export function useCountdown(initial = 0): [number, (seconds: number) => void] {
  const [seconds, setSeconds] = useState(initial);

  useEffect(() => {
    if (seconds <= 0) return;
    const timer = setTimeout(() => setSeconds((current) => current - 1), 1000);
    return () => clearTimeout(timer);
  }, [seconds]);

  return [seconds, setSeconds];
}

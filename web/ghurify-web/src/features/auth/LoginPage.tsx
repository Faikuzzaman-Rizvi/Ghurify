import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Navigate, useNavigate } from 'react-router';

import { ApiError, asNumber } from '@/api/client';
import { Scenery } from '@/components/Scenery';
import { useAuthStore } from './authStore';
import { useRequestOtp, useVerifyOtp } from './useAuthMutations';

/**
 * Mirrors the backend validator: one @, something before it, a dotted domain after it.
 * Keeping the shapes the same here and on the server means the user is told about a bad
 * address before a request is made, not after.
 */
const emailSchema = z.object({
  email: z
    .string()
    .trim()
    .max(254)
    .refine((value) => /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/.test(value), { message: 'invalid' }),
});

const codeSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{6}$/, { message: 'invalid' }),
});

type EmailForm = z.infer<typeof emailSchema>;
type CodeForm = z.infer<typeof codeSchema>;

export function LoginPage() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);

  const [email, setEmail] = useState<string | null>(null);
  const [resendIn, setResendIn] = useState(0);

  // Already signed in: nothing to do here.
  if (status === 'authenticated') {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="mx-auto grid max-w-5xl gap-6 px-4 py-10 md:grid-cols-2 md:py-16">
      <aside className="relative hidden overflow-hidden rounded-4xl bg-deep md:block">
        <Scenery kind="Lake" className="absolute inset-0 h-full w-full" />
        <div className="absolute inset-0 bg-linear-to-t from-deep/90 via-deep/20 to-transparent" />
        <div className="absolute inset-x-0 bottom-0 p-8 text-white">
          <h2 className="text-3xl font-extrabold">{t('auth.sideTitle')}</h2>
          <p className="mt-2 text-white/85">{t('auth.sideBody')}</p>
        </div>
      </aside>

      <section className="flex flex-col justify-center gap-8 rounded-4xl bg-white p-6 shadow-xl ring-1 ring-hill/10 sm:p-10">
        <header>
          <h1 className="text-3xl font-extrabold text-hill">{t('auth.title')}</h1>
          <p className="mt-1 text-sm text-deep/70">{t('auth.subtitle')}</p>
        </header>

        {email === null ? (
          <EmailStep
            onCodeSent={(sentTo, resendAfterSeconds) => {
              setEmail(sentTo);
              setResendIn(resendAfterSeconds);
            }}
          />
        ) : (
          <CodeStep
            email={email}
            resendIn={resendIn}
            onResendCountdown={setResendIn}
            onChangeEmail={() => setEmail(null)}
          />
        )}
      </section>
    </div>
  );
}

function EmailStep({
  onCodeSent,
}: {
  onCodeSent: (email: string, resendAfterSeconds: number) => void;
}) {
  const { t } = useTranslation();
  const requestOtp = useRequestOtp();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EmailForm>({ resolver: zodResolver(emailSchema) });

  // mutate, not mutateAsync: a rejected mutateAsync would escape the submit handler as an
  // unhandled rejection. The failure is already rendered from requestOtp.isError below.
  const onSubmit = handleSubmit(({ email }) => {
    requestOtp.mutate(email, {
      onSuccess: (result) => onCodeSent(email, asNumber(result.resendAfterSeconds)),
    });
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
      <div className="flex flex-col gap-1">
        <label htmlFor="email" className="text-sm font-medium text-deep">
          {t('auth.emailLabel')}
        </label>
        <input
          id="email"
          type="email"
          inputMode="email"
          autoComplete="email"
          autoCapitalize="none"
          spellCheck={false}
          placeholder="rizvi@example.com"
          aria-invalid={errors.email ? 'true' : 'false'}
          aria-describedby={errors.email ? 'email-error' : 'email-hint'}
          className="rounded-2xl border border-hill/25 bg-mist px-4 py-3 text-base focus:border-hill focus:bg-white"
          {...register('email')}
        />
        <p id="email-hint" className="text-xs text-deep/60">
          {t('auth.emailHint')}
        </p>
        {errors.email && (
          <p id="email-error" role="alert" className="text-sm text-jamdani">
            {t('auth.emailInvalid')}
          </p>
        )}
      </div>

      {requestOtp.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorMessage(requestOtp.error, t)}
        </p>
      )}

      <button
        type="submit"
        disabled={requestOtp.isPending}
        className="rounded-2xl bg-hill px-4 py-3 font-display text-lg font-bold text-white transition hover:bg-deep disabled:opacity-60"
      >
        {requestOtp.isPending ? t('common.loading') : t('auth.sendCode')}
      </button>
    </form>
  );
}

function CodeStep({
  email,
  resendIn,
  onResendCountdown,
  onChangeEmail,
}: {
  email: string;
  resendIn: number;
  onResendCountdown: (seconds: number) => void;
  onChangeEmail: () => void;
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const verifyOtp = useVerifyOtp();
  const requestOtp = useRequestOtp();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CodeForm>({ resolver: zodResolver(codeSchema) });

  // Counts the resend button back in, one second at a time.
  useEffect(() => {
    if (resendIn <= 0) {
      return;
    }

    const timer = setTimeout(() => onResendCountdown(resendIn - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendIn, onResendCountdown]);

  const onSubmit = handleSubmit(({ code }) => {
    verifyOtp.mutate({ email, code }, { onSuccess: () => void navigate('/', { replace: true }) });
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
      <div>
        <p className="text-sm text-deep/70">{t('auth.codeSentTo', { email })}</p>
        {/* Mail lands in spam often enough that saying so up front saves a support message. */}
        <p className="mt-1 text-xs text-deep/60">{t('auth.checkInbox')}</p>
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="code" className="text-sm font-medium text-deep">
          {t('auth.codeLabel')}
        </label>
        <input
          id="code"
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          aria-invalid={errors.code ? 'true' : 'false'}
          aria-describedby={errors.code ? 'code-error' : undefined}
          className="rounded-2xl border border-hill/25 bg-mist px-4 py-3 text-center text-2xl tracking-[0.4em] focus:border-hill focus:bg-white"
          {...register('code')}
        />
        {errors.code && (
          <p id="code-error" role="alert" className="text-sm text-jamdani">
            {t('auth.codeInvalid')}
          </p>
        )}
      </div>

      {verifyOtp.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorMessage(verifyOtp.error, t)}
        </p>
      )}

      <button
        type="submit"
        disabled={verifyOtp.isPending}
        className="rounded-2xl bg-hill px-4 py-3 font-display text-lg font-bold text-white transition hover:bg-deep disabled:opacity-60"
      >
        {verifyOtp.isPending ? t('common.loading') : t('auth.verify')}
      </button>

      <div className="flex items-center justify-between text-sm">
        <button
          type="button"
          onClick={onChangeEmail}
          className="text-hill underline underline-offset-4"
        >
          {t('auth.changeEmail')}
        </button>

        <button
          type="button"
          disabled={resendIn > 0 || requestOtp.isPending}
          onClick={() => {
            requestOtp.mutate(email, {
              onSuccess: (result) => onResendCountdown(asNumber(result.resendAfterSeconds)),
            });
          }}
          className="text-hill underline underline-offset-4 disabled:text-deep/40 disabled:no-underline"
        >
          {resendIn > 0 ? t('auth.resendIn', { seconds: resendIn }) : t('auth.resend')}
        </button>
      </div>
    </form>
  );
}

/** Turns an API failure into something a traveller can act on. */
function errorMessage(error: unknown, t: (key: string) => string): string {
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return t('auth.tooManyRequests');
    }

    if (error.status === 401) {
      return t('auth.codeRejected');
    }

    if (error.status === 403) {
      return t('auth.accountUnavailable');
    }
  }

  return t('common.error');
}

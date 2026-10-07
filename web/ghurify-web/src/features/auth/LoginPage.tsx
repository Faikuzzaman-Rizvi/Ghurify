import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';
import { ArrowRight, KeyRound, LockKeyhole, Mail, UserPlus } from 'lucide-react';

import { ApiError } from '@/api/client';
import { AuthField, AuthShell, FormError, PasswordField } from './AuthShell';
import { authButtonClass, emailField, useReturnTo } from './authForm';
import { useAuthStore } from './authStore';
import { useResendCode, useSignIn } from './useAuthMutations';

const schema = z.object({
  email: emailField,
  password: z.string().min(1, { message: 'passwordRequired' }).max(128),
});

type SignInForm = z.infer<typeof schema>;

/** Sign in with the permanent email address and password. No code. */
export function LoginPage() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const navigate = useNavigate();
  const location = useLocation();
  const returnTo = useReturnTo();
  const signIn = useSignIn();
  const resend = useResendCode();
  const prefilled = (location.state as { email?: string } | null)?.email ?? '';

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
  } = useForm<SignInForm>({
    resolver: zodResolver(schema),
    defaultValues: { email: prefilled, password: '' },
  });

  // Already signed in: nothing to do here.
  if (status === 'authenticated') {
    return <Navigate to={returnTo} replace />;
  }

  const onSubmit = handleSubmit(({ email, password }) => {
    signIn.mutate(
      { email, password },
      {
        onSuccess: () => void navigate(returnTo, { replace: true }),
        onError: (error) => {
          // An admin asked for a new password: go straight to the reset, address filled in.
          if (error instanceof ApiError && error.code === 'password_reset_required') {
            void navigate('/forgot-password', { state: { email, from: returnTo } });
          }
        },
      },
    );
  });

  const notConfirmed =
    signIn.error instanceof ApiError && signIn.error.code === 'email_not_confirmed';

  return (
    <AuthShell
      icon={<LockKeyhole aria-hidden="true" className="h-6 w-6" />}
      title={t('auth.title')}
      subtitle={t('auth.subtitle')}
    >
      <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-5" noValidate>
        <AuthField
          id="email"
          type="email"
          inputMode="email"
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          placeholder="rizvi@example.com"
          label={t('auth.emailLabel')}
          error={errors.email ? t('auth.emailInvalid') : undefined}
          field={register('email')}
          leading={Mail}
        />

        {/* The reset link sits level with the password's label, where people look for it. */}
        <div className="relative">
          <PasswordField
            id="password"
            label={t('auth.passwordLabel')}
            autoComplete="current-password"
            error={errors.password ? t('auth.passwordRequired') : undefined}
            field={register('password')}
            leading={KeyRound}
          />
          <Link
            to="/forgot-password"
            state={{ email: getValues('email'), from: returnTo }}
            className="absolute right-0 top-0 text-sm font-medium text-hill transition hover:text-deep hover:underline hover:underline-offset-4"
          >
            {t('auth.forgotPassword')}
          </Link>
        </div>

        {signIn.isError && !notConfirmed && <FormError error={signIn.error} />}

        {notConfirmed && (
          <div
            role="alert"
            className="flex flex-col gap-2 rounded-xl bg-turmeric/15 p-3 text-sm text-deep"
          >
            <p>{t('errors.email_not_confirmed')}</p>
            <button
              type="button"
              disabled={resend.isPending}
              onClick={() => {
                const email = getValues('email').trim();
                resend.mutate(email, {
                  onSuccess: () =>
                    void navigate('/register', {
                      state: { email, step: 'confirm', from: returnTo },
                    }),
                });
              }}
              className="self-start font-semibold text-hill underline underline-offset-4"
            >
              {t('auth.sendNewCode')}
            </button>
            <FormError error={resend.error} />
          </div>
        )}

        <button
          type="submit"
          disabled={signIn.isPending}
          className={`${authButtonClass} group mt-1`}
        >
          {signIn.isPending ? t('common.loading') : t('auth.signIn')}
          {!signIn.isPending && (
            <ArrowRight
              aria-hidden="true"
              className="h-4 w-4 transition-transform group-hover:translate-x-0.5"
            />
          )}
        </button>
      </form>

      <div className="flex flex-col gap-3">
        <div className="flex items-center gap-3 rounded-2xl bg-mist p-4 ring-1 ring-hill/10">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white text-hill shadow-sm">
            <UserPlus aria-hidden="true" className="h-5 w-5" />
          </span>
          <p className="text-sm text-deep/75">
            {t('auth.noAccount')}{' '}
            <Link
              to="/register"
              state={{ from: returnTo }}
              className="font-semibold text-hill underline-offset-4 hover:underline"
            >
              {t('auth.createAccount')}
            </Link>
          </p>
        </div>
        <p className="text-center text-xs leading-relaxed text-deep/50">{t('auth.legacyHint')}</p>
      </div>
    </AuthShell>
  );
}

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';
import { KeyRound } from 'lucide-react';

import { asNumber } from '@/api/client';
import { AuthField, AuthShell, CodeField, FormError, PasswordField } from './AuthShell';
import {
  authButtonClass,
  codeField,
  emailField,
  newPasswordField,
  useCountdown,
  useReturnTo,
} from './authForm';
import { useForgotPassword, useResetPassword } from './useAuthMutations';

const emailSchema = z.object({ email: emailField });
const resetSchema = z.object({ code: codeField, newPassword: newPasswordField });

type EmailForm = z.infer<typeof emailSchema>;
type ResetForm = z.infer<typeof resetSchema>;

/**
 * Forgotten password: a code to the email, then a new password, which signs in here and out
 * everywhere else. Also how someone who used to sign in with a code sets their first password.
 */
export function ForgotPasswordPage() {
  const { t } = useTranslation();
  const location = useLocation();
  const returnTo = useReturnTo();
  const prefilled = (location.state as { email?: string } | null)?.email ?? '';
  const [email, setEmail] = useState<string | null>(null);
  const [resendIn, setResendIn] = useCountdown();

  return (
    <AuthShell
      icon={<KeyRound aria-hidden="true" className="h-6 w-6" />}
      title={email === null ? t('auth.forgot.title') : t('auth.reset.title')}
      subtitle={email === null ? t('auth.forgot.subtitle') : t('auth.reset.sentTo', { email })}
    >
      {email === null ? (
        <EmailStep
          initialEmail={prefilled}
          onCodeSent={(sentTo, resendAfter) => {
            setEmail(sentTo);
            setResendIn(resendAfter);
          }}
        />
      ) : (
        <ResetStep
          email={email}
          resendIn={resendIn}
          onResent={setResendIn}
          onChangeEmail={() => setEmail(null)}
          returnTo={returnTo}
        />
      )}
      <Link
        to="/login"
        state={{ from: returnTo }}
        className="text-sm text-hill underline underline-offset-4"
      >
        {t('auth.backToSignIn')}
      </Link>
    </AuthShell>
  );
}

function EmailStep({
  initialEmail,
  onCodeSent,
}: {
  initialEmail: string;
  onCodeSent: (email: string, resendAfterSeconds: number) => void;
}) {
  const { t } = useTranslation();
  const forgot = useForgotPassword();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EmailForm>({
    resolver: zodResolver(emailSchema),
    defaultValues: { email: initialEmail },
  });

  const onSubmit = handleSubmit(({ email }) => {
    forgot.mutate(email, {
      onSuccess: (sent) => onCodeSent(email, asNumber(sent.resendAfterSeconds)),
    });
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
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
      />
      <FormError error={forgot.error} />
      <button type="submit" disabled={forgot.isPending} className={authButtonClass}>
        {forgot.isPending ? t('common.loading') : t('auth.forgot.submit')}
      </button>
    </form>
  );
}

function ResetStep({
  email,
  resendIn,
  onResent,
  onChangeEmail,
  returnTo,
}: {
  email: string;
  resendIn: number;
  onResent: (seconds: number) => void;
  onChangeEmail: () => void;
  returnTo: string;
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const reset = useResetPassword();
  const forgot = useForgotPassword();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ResetForm>({ resolver: zodResolver(resetSchema) });

  const onSubmit = handleSubmit(({ code, newPassword }) => {
    reset.mutate(
      { email, code, newPassword },
      { onSuccess: () => void navigate(returnTo, { replace: true }) },
    );
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
      <p className="text-xs text-deep/60">{t('auth.checkInbox')}</p>

      <CodeField error={errors.code ? t('auth.codeInvalid') : undefined} field={register('code')} />
      <PasswordField
        id="newPassword"
        label={t('auth.newPasswordLabel')}
        autoComplete="new-password"
        hint={t('auth.passwordHint')}
        error={
          errors.newPassword
            ? t(`auth.${errors.newPassword.message ?? 'passwordTooShort'}`)
            : undefined
        }
        field={register('newPassword')}
      />

      <FormError error={reset.error} />
      <FormError error={forgot.error} />

      <button type="submit" disabled={reset.isPending} className={authButtonClass}>
        {reset.isPending ? t('common.loading') : t('auth.reset.submit')}
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
          disabled={resendIn > 0 || forgot.isPending}
          onClick={() =>
            forgot.mutate(email, {
              onSuccess: (sent) => onResent(asNumber(sent.resendAfterSeconds)),
            })
          }
          className="text-hill underline underline-offset-4 disabled:text-deep/40 disabled:no-underline"
        >
          {resendIn > 0 ? t('auth.resendIn', { seconds: resendIn }) : t('auth.resend')}
        </button>
      </div>
    </form>
  );
}

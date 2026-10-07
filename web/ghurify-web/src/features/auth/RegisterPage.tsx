import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';
import { MailCheck, UserPlus } from 'lucide-react';

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
import { useAuthStore } from './authStore';
import { useConfirmEmail, useRegister, useResendCode } from './useAuthMutations';

const detailsSchema = z.object({
  displayName: z
    .string()
    .trim()
    .min(2, { message: 'nameInvalid' })
    .max(100, { message: 'nameInvalid' }),
  email: emailField,
  password: newPasswordField,
});

const codeSchema = z.object({ code: codeField });

type DetailsForm = z.infer<typeof detailsSchema>;
type CodeForm = z.infer<typeof codeSchema>;

/**
 * Create an account: name, email (permanent) and password, then the six-digit code emailed to
 * confirm the address. The code is needed this once; after that, sign-in is just the password.
 */
export function RegisterPage() {
  const { t } = useTranslation();
  const status = useAuthStore((state) => state.status);
  const location = useLocation();
  const returnTo = useReturnTo();
  const arrived = location.state as { email?: string; step?: string } | null;

  const [email, setEmail] = useState<string | null>(
    arrived?.step === 'confirm' && arrived.email ? arrived.email : null,
  );
  const [resendIn, setResendIn] = useCountdown(email ? 60 : 0);

  if (status === 'authenticated') {
    return <Navigate to={returnTo} replace />;
  }

  return email === null ? (
    <AuthShell
      photo="saint-martins"
      icon={<UserPlus aria-hidden="true" className="h-6 w-6" />}
      title={t('auth.register.title')}
      subtitle={t('auth.register.subtitle')}
    >
      <DetailsStep
        initialEmail={arrived?.email ?? ''}
        onCodeSent={(sentTo, resendAfter) => {
          setEmail(sentTo);
          setResendIn(resendAfter);
        }}
      />
      <p className="border-t border-hill/10 pt-6 text-sm text-deep/80">
        {t('auth.haveAccount')}{' '}
        <Link
          to="/login"
          state={{ from: returnTo }}
          className="font-semibold text-hill underline underline-offset-4"
        >
          {t('auth.signIn')}
        </Link>
      </p>
    </AuthShell>
  ) : (
    <AuthShell
      photo="saint-martins"
      icon={<MailCheck aria-hidden="true" className="h-6 w-6" />}
      title={t('auth.confirm.title')}
      subtitle={t('auth.codeSentTo', { email })}
    >
      <ConfirmStep
        email={email}
        resendIn={resendIn}
        onResent={setResendIn}
        onChangeEmail={() => setEmail(null)}
        returnTo={returnTo}
      />
    </AuthShell>
  );
}

function DetailsStep({
  initialEmail,
  onCodeSent,
}: {
  initialEmail: string;
  onCodeSent: (email: string, resendAfterSeconds: number) => void;
}) {
  const { t } = useTranslation();
  const registerAccount = useRegister();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DetailsForm>({
    resolver: zodResolver(detailsSchema),
    defaultValues: { displayName: '', email: initialEmail, password: '' },
  });

  const onSubmit = handleSubmit(({ displayName, email, password }) => {
    registerAccount.mutate(
      { displayName, email, password },
      { onSuccess: (sent) => onCodeSent(email, asNumber(sent.resendAfterSeconds)) },
    );
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
      <AuthField
        id="displayName"
        autoComplete="name"
        label={t('auth.nameLabel')}
        hint={t('auth.nameHint')}
        error={errors.displayName ? t('auth.nameInvalid') : undefined}
        field={register('displayName')}
      />
      <AuthField
        id="email"
        type="email"
        inputMode="email"
        autoComplete="username"
        autoCapitalize="none"
        spellCheck={false}
        placeholder="rizvi@example.com"
        label={t('auth.emailLabel')}
        hint={t('auth.emailHint')}
        error={errors.email ? t('auth.emailInvalid') : undefined}
        field={register('email')}
      />
      <PasswordField
        id="password"
        label={t('auth.passwordLabel')}
        autoComplete="new-password"
        hint={t('auth.passwordHint')}
        error={
          errors.password ? t(`auth.${errors.password.message ?? 'passwordTooShort'}`) : undefined
        }
        field={register('password')}
      />

      <FormError error={registerAccount.error} />

      <button type="submit" disabled={registerAccount.isPending} className={authButtonClass}>
        {registerAccount.isPending ? t('common.loading') : t('auth.register.submit')}
      </button>
    </form>
  );
}

function ConfirmStep({
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
  const confirm = useConfirmEmail();
  const resend = useResendCode();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CodeForm>({ resolver: zodResolver(codeSchema) });

  const onSubmit = handleSubmit(({ code }) => {
    confirm.mutate(
      { email, code },
      { onSuccess: () => void navigate(returnTo, { replace: true }) },
    );
  });

  return (
    <form onSubmit={(event) => void onSubmit(event)} className="flex flex-col gap-4" noValidate>
      {/* Mail lands in spam often enough that saying so up front saves a support message. */}
      <p className="text-xs text-deep/60">{t('auth.checkInbox')}</p>

      <CodeField error={errors.code ? t('auth.codeInvalid') : undefined} field={register('code')} />

      <FormError error={confirm.error} />
      <FormError error={resend.error} />

      <button type="submit" disabled={confirm.isPending} className={authButtonClass}>
        {confirm.isPending ? t('common.loading') : t('auth.confirm.submit')}
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
          disabled={resendIn > 0 || resend.isPending}
          onClick={() =>
            resend.mutate(email, {
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

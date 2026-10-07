import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { KeyRound } from 'lucide-react';

import { cardClass, primaryButtonClass } from '@/components/Field';
import { FormError, PasswordField } from './AuthShell';
import { newPasswordField } from './authForm';
import { useChangePassword } from './useAuthMutations';

const schema = z.object({
  currentPassword: z.string().max(128),
  newPassword: newPasswordField,
});

type PasswordForm = z.infer<typeof schema>;

/** Change the password. Every other device is signed out; this one stays signed in. */
export function PasswordCard() {
  const { t } = useTranslation();
  const change = useChangePassword();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<PasswordForm>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '' },
  });

  const onSubmit = handleSubmit((values) => {
    change.mutate(values, { onSuccess: () => reset() });
  });

  return (
    <section className={cardClass} aria-labelledby="password-title">
      <div className="flex items-start gap-4">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-hill text-white">
          <KeyRound aria-hidden="true" className="h-6 w-6" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 id="password-title" className="text-xl font-semibold">
            {t('account.password.title')}
          </h2>
          <p className="mt-1 text-sm text-deep/70">{t('account.password.lead')}</p>
        </div>
      </div>

      <form
        onSubmit={(event) => void onSubmit(event)}
        className="mt-6 grid gap-4 sm:grid-cols-2"
        noValidate
      >
        <PasswordField
          id="currentPassword"
          label={t('account.password.current')}
          autoComplete="current-password"
          field={register('currentPassword')}
        />
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
        <div className="flex flex-col gap-2 sm:col-span-2">
          <FormError error={change.error} />
          {change.isSuccess && (
            <p role="status" className="text-sm text-hill">
              {t('account.password.changed')}
            </p>
          )}
          <button
            type="submit"
            disabled={change.isPending}
            className={`${primaryButtonClass} self-start`}
          >
            {change.isPending ? t('common.saving') : t('account.password.submit')}
          </button>
        </div>
      </form>
    </section>
  );
}

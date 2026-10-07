import { useState, type InputHTMLAttributes, type ReactNode } from 'react';
import type { UseFormRegisterReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { CircleCheck, Eye, EyeOff } from 'lucide-react';

import { Photo } from '@/components/ui/Photo';
import { authErrorText, authInputClass } from './authForm';

/** The frame of every account screen: a photo panel on wide screens, and the form card. */
export function AuthShell({
  icon,
  title,
  subtitle,
  children,
}: {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  const { t } = useTranslation();

  return (
    <div className="grid min-h-[calc(100svh-4.5rem)] lg:grid-cols-2">
      <aside className="relative isolate hidden overflow-hidden bg-night lg:block">
        <Photo
          slug="sajek"
          kind="Hills"
          cut="wide"
          sizes="50vw"
          className="absolute! inset-0 -z-10"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-10 bg-linear-to-t from-night via-night/40 to-night/10"
        />
        <div className="absolute inset-x-0 bottom-0 p-12 text-white xl:p-16">
          <p className="eyebrow text-dusk!">{t('app.tagline')}</p>
          <h2 className="mt-3 text-4xl font-bold text-white!">{t('auth.sideTitle')}</h2>
          <p className="mt-3 max-w-md text-lg text-white/85">{t('auth.sideBody')}</p>
          <ul className="mt-8 flex flex-wrap gap-x-6 gap-y-3 text-sm text-white/90">
            {(['verified', 'escrow', 'sos'] as const).map((key) => (
              <li key={key} className="flex items-center gap-2">
                <CircleCheck aria-hidden="true" className="h-4 w-4 text-dusk" />
                {t(`home.promise.${key}`)}
              </li>
            ))}
          </ul>
        </div>
      </aside>

      <section className="flex items-center justify-center bg-mist px-4 py-12 sm:px-8 lg:bg-white">
        <div className="flex w-full max-w-md flex-col gap-8 rounded-2xl bg-white p-6 shadow-xl ring-1 ring-hill/10 sm:p-10 lg:shadow-none lg:ring-0">
          <header>
            <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-hill/10 text-hill">
              {icon}
            </span>
            <h1 className="mt-5 text-3xl font-bold">{title}</h1>
            {subtitle && <p className="mt-2 text-deep/70">{subtitle}</p>}
          </header>
          {children}
        </div>
      </section>
    </div>
  );
}

/** A labelled text field with its error, for the account forms. */
export function AuthField({
  id,
  label,
  hint,
  error,
  field,
  ...input
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string | undefined;
  field: UseFormRegisterReturn;
} & Omit<InputHTMLAttributes<HTMLInputElement>, 'id'>) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-deep">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={describedBy}
        className={authInputClass}
        {...input}
        {...field}
      />
      {hint && !error && (
        <p id={`${id}-hint`} className="text-xs text-deep/60">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm text-jamdani">
          {error}
        </p>
      )}
    </div>
  );
}

/** A password field with a show/hide toggle, so people can check what they typed on a phone. */
export function PasswordField({
  id,
  label,
  hint,
  error,
  field,
  autoComplete,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string | undefined;
  field: UseFormRegisterReturn;
  autoComplete: 'current-password' | 'new-password';
}) {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-deep">
        {label}
      </label>
      <div className="relative">
        <input
          id={id}
          type={visible ? 'text' : 'password'}
          autoComplete={autoComplete}
          autoCapitalize="none"
          spellCheck={false}
          maxLength={128}
          aria-invalid={error ? 'true' : 'false'}
          aria-describedby={describedBy}
          className={`${authInputClass} pr-12`}
          {...field}
        />
        <button
          type="button"
          onClick={() => setVisible((shown) => !shown)}
          aria-label={visible ? t('auth.hidePassword') : t('auth.showPassword')}
          aria-pressed={visible}
          className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-deep/60 hover:text-deep"
        >
          {visible ? (
            <EyeOff aria-hidden="true" className="h-5 w-5" />
          ) : (
            <Eye aria-hidden="true" className="h-5 w-5" />
          )}
        </button>
      </div>
      {hint && !error && (
        <p id={`${id}-hint`} className="text-xs text-deep/60">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm text-jamdani">
          {error}
        </p>
      )}
    </div>
  );
}

/** The six-digit code field: big, numeric keypad, and the OS can fill it from the email. */
export function CodeField({
  error,
  field,
}: {
  error?: string | undefined;
  field: UseFormRegisterReturn;
}) {
  const { t } = useTranslation();

  return (
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
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={error ? 'code-error' : undefined}
        className={`${authInputClass} text-center text-2xl tracking-[0.4em]`}
        {...field}
      />
      {error && (
        <p id="code-error" role="alert" className="text-sm text-jamdani">
          {error}
        </p>
      )}
    </div>
  );
}

/** A form error message from a failed request. */
export function FormError({ error }: { error: unknown }) {
  const { t } = useTranslation();
  if (!error) return null;

  return (
    <p role="alert" className="text-sm text-jamdani">
      {authErrorText(error, t)}
    </p>
  );
}

import { useState, type InputHTMLAttributes, type ReactNode } from 'react';
import type { UseFormRegisterReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { BadgeCheck, Eye, EyeOff, LockKeyhole, Siren, type LucideIcon } from 'lucide-react';

import { useHeroUnderHeader } from '@/components/ui/headerStore';
import { BangladeshMap } from '@/components/ui/BangladeshMap';
import { Photo } from '@/components/ui/Photo';
import { authErrorText, authInputClass } from './authForm';

const promises: { key: 'verified' | 'escrow' | 'sos'; icon: LucideIcon }[] = [
  { key: 'verified', icon: BadgeCheck },
  { key: 'escrow', icon: LockKeyhole },
  { key: 'sos', icon: Siren },
];

/**
 * The frame of every account screen (sign in, create an account, reset a password): one
 * full-bleed photo behind the whole page, with the header floating over it. On wide screens the
 * welcome and the safety promises sit on the left; the form is a white card on the right. On
 * phones the card fills the width over the same photo.
 */
export function AuthShell({
  icon,
  title,
  subtitle,
  children,
  photo = 'sajek',
}: {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  children: ReactNode;
  /** Which destination photo stands behind the page. */
  photo?: string;
}) {
  const { t } = useTranslation();
  useHeroUnderHeader();

  return (
    <section className="relative isolate flex min-h-svh items-center overflow-hidden bg-night">
      <Photo
        slug={photo}
        kind="Hills"
        cut="wide"
        priority
        className="absolute! inset-0 -z-10"
        imgClassName="animate-ken-burns"
      />
      {/* Dark where the words are, lighter towards the card, and a floor for the bottom edge. */}
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-r from-night/95 via-night/70 to-night/40"
      />
      <div
        aria-hidden="true"
        className="absolute inset-x-0 bottom-0 -z-10 h-1/3 bg-linear-to-t from-night/80 to-transparent"
      />
      <BangladeshMap
        tone="dark"
        showDestinations
        className="pointer-events-none absolute -bottom-24 -left-20 -z-10 hidden h-[80vh] w-auto opacity-15 lg:block"
      />

      <div className="container-page grid w-full items-center gap-10 pb-14 pt-28 lg:grid-cols-[1fr_minmax(0,28rem)] lg:gap-16 lg:pb-20 lg:pt-32">
        <div className="hidden text-white lg:block">
          <p className="eyebrow animate-rise text-dusk!">{t('app.tagline')}</p>
          <h2 className="mt-4 max-w-xl animate-rise text-5xl font-bold leading-[1.1] text-white! [animation-delay:100ms]">
            {t('auth.sideTitle')}
          </h2>
          <p className="mt-5 max-w-lg animate-rise text-lg leading-relaxed text-white/80 [animation-delay:200ms]">
            {t('auth.sideBody')}
          </p>

          <ul className="mt-10 grid max-w-xl animate-rise gap-3 [animation-delay:300ms]">
            {promises.map(({ key, icon: Icon }) => (
              <li
                key={key}
                className="flex items-center gap-4 rounded-2xl bg-white/[0.07] p-4 ring-1 ring-white/10 backdrop-blur-md"
              >
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-turmeric text-night shadow-[0_8px_20px_rgba(217,154,18,0.3)]">
                  <Icon aria-hidden="true" className="h-5 w-5" />
                </span>
                <span>
                  <span className="block font-display font-semibold text-white">
                    {t(`home.safety.${key}.title`)}
                  </span>
                  <span className="block text-sm text-white/70">{t(`footer.promise.${key}`)}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="mx-auto w-full max-w-md animate-rise [animation-delay:150ms] lg:mx-0">
          {/* Phones: the welcome, short, above the card. */}
          <p className="eyebrow mb-4 text-center text-dusk! lg:hidden">{t('app.tagline')}</p>

          <div className="relative overflow-hidden rounded-3xl bg-white p-7 shadow-[0_30px_80px_rgba(0,0,0,0.35)] ring-1 ring-white/40 sm:p-10">
            <span
              aria-hidden="true"
              className="absolute inset-x-0 top-0 h-1.5 bg-linear-to-r from-turmeric via-dusk to-hill"
            />
            <header className="mb-8">
              <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-linear-to-br from-hill to-deep text-white shadow-[0_10px_24px_rgba(36,92,67,0.35)]">
                {icon}
              </span>
              <h1 className="mt-6 text-3xl font-bold leading-tight">{title}</h1>
              {subtitle && <p className="mt-2 leading-relaxed text-deep/65">{subtitle}</p>}
            </header>
            <div className="flex flex-col gap-7">{children}</div>
          </div>
        </div>
      </div>
    </section>
  );
}

/** Label, an optional icon inside the field, the field, and its hint or error. */
function FieldFrame({
  id,
  label,
  hint,
  error,
  leading,
  children,
}: {
  id: string;
  label: string;
  hint?: string | undefined;
  error?: string | undefined;
  leading?: LucideIcon | undefined;
  children: ReactNode;
}) {
  const Leading = leading;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-deep">
        {label}
      </label>
      <div className="relative">
        {Leading && (
          <Leading
            aria-hidden="true"
            className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-deep/40"
          />
        )}
        {children}
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

/** A labelled text field with its error, for the account forms. */
export function AuthField({
  id,
  label,
  hint,
  error,
  field,
  leading,
  ...input
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string | undefined;
  field: UseFormRegisterReturn;
  /** An icon drawn inside the field, before the text. */
  leading?: LucideIcon;
} & Omit<InputHTMLAttributes<HTMLInputElement>, 'id'>) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <FieldFrame id={id} label={label} hint={hint} error={error} leading={leading}>
      <input
        id={id}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={describedBy}
        className={`${authInputClass} ${leading ? 'pl-11' : ''}`}
        {...input}
        {...field}
      />
    </FieldFrame>
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
  leading,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string | undefined;
  field: UseFormRegisterReturn;
  autoComplete: 'current-password' | 'new-password';
  leading?: LucideIcon;
}) {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <FieldFrame id={id} label={label} hint={hint} error={error} leading={leading}>
      <input
        id={id}
        type={visible ? 'text' : 'password'}
        autoComplete={autoComplete}
        autoCapitalize="none"
        spellCheck={false}
        maxLength={128}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={describedBy}
        className={`${authInputClass} pr-12 ${leading ? 'pl-11' : ''}`}
        {...field}
      />
      <button
        type="button"
        onClick={() => setVisible((shown) => !shown)}
        aria-label={visible ? t('auth.hidePassword') : t('auth.showPassword')}
        aria-pressed={visible}
        className="absolute inset-y-0 right-0 flex w-12 items-center justify-center rounded-r-xl text-deep/50 transition hover:text-hill"
      >
        {visible ? (
          <EyeOff aria-hidden="true" className="h-5 w-5" />
        ) : (
          <Eye aria-hidden="true" className="h-5 w-5" />
        )}
      </button>
    </FieldFrame>
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
    <FieldFrame id="code" label={t('auth.codeLabel')} error={error}>
      <input
        id="code"
        type="text"
        inputMode="numeric"
        autoComplete="one-time-code"
        maxLength={6}
        aria-invalid={error ? 'true' : 'false'}
        aria-describedby={error ? 'code-error' : undefined}
        className={`${authInputClass} text-center font-display text-2xl font-semibold tracking-[0.5em]`}
        {...field}
      />
    </FieldFrame>
  );
}

/** A form error message from a failed request. */
export function FormError({ error }: { error: unknown }) {
  const { t } = useTranslation();
  if (!error) return null;

  return (
    <p
      role="alert"
      className="rounded-xl bg-jamdani/5 px-4 py-3 text-sm text-jamdani ring-1 ring-jamdani/15"
    >
      {authErrorText(error, t)}
    </p>
  );
}

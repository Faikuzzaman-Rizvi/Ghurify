import { useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  BadgeCheck,
  Check,
  ChevronRight,
  HeartPulse,
  LogOut,
  Map,
  NotebookPen,
  ShieldCheck,
  Tent,
  UserRound,
  Wallet,
  type LucideIcon,
} from 'lucide-react';

import { ErrorState } from '@/components/States';
import { PasswordCard } from './PasswordCard';
import { AvatarEditor } from './AvatarEditor';
import {
  cardClass,
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextAreaField,
  TextField,
} from '@/components/Field';
import { PageBanner } from '@/components/ui/PageBanner';
import { VerificationBadge } from '@/components/VerificationBadge';
import { errorText } from '@/lib/errors';
import { formatCount, formatMonthYear, toLanguage } from '@/lib/format';
import { isBangladeshiMobile } from '@/lib/phone';
import { useLogout } from './useAuthMutations';
import { genders, type Profile, type VerificationLevel } from './profileApi';
import { useBecomeHost, useMyProfile, useMyVerifications, useUpdateProfile } from './useProfile';

/** Mirrors UpdateProfileCommandValidator on the server. */
const profileSchema = z
  .object({
    displayName: z.string().trim().min(2, 'name').max(100, 'name'),
    gender: z.enum(['', 'Female', 'Male', 'Other']),
    phone: z
      .string()
      .trim()
      .refine((value) => value === '' || isBangladeshiMobile(value), 'phone'),
    homeDistrict: z.string().trim().max(60),
    bio: z.string().trim().max(500, 'bio'),
    emergencyContactName: z.string().trim().max(100),
    emergencyContactPhone: z
      .string()
      .trim()
      .refine((value) => value === '' || isBangladeshiMobile(value), 'phone'),
  })
  .refine((form) => form.emergencyContactName === '' || form.emergencyContactPhone !== '', {
    path: ['emergencyContactPhone'],
    message: 'contactPhone',
  });

type ProfileForm = z.infer<typeof profileSchema>;

/** The identity checks in the order they build on each other. */
const levels: readonly VerificationLevel[] = ['Phone', 'Nid', 'NidSelfie'];

/** The signed-in user's own page: profile, identity checks and hosting. */
export function AccountPage() {
  const { t } = useTranslation();
  const { data: profile, isPending, isError, refetch } = useMyProfile();

  if (isPending) {
    return (
      <div role="status">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-80 animate-pulse bg-hill/15" />
        <div
          aria-hidden="true"
          className="container-page -mt-16 grid gap-8 lg:grid-cols-[20rem_1fr]"
        >
          <div className="h-96 animate-pulse rounded-2xl bg-hill/10" />
          <div className="h-96 animate-pulse rounded-2xl bg-hill/10" />
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="container-page max-w-3xl py-16">
        <ErrorState message={t('account.loadError')} onRetry={() => void refetch()} />
      </div>
    );
  }

  return <AccountView profile={profile} />;
}

function AccountView({ profile }: { profile: Profile }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const logout = useLogout();

  return (
    <>
      <PageBanner
        compact
        slug="bandarban"
        kind="Hills"
        eyebrow={t('account.eyebrow')}
        title={profile.displayName ?? t('account.title')}
        aside={
          <button
            type="button"
            onClick={() => logout.mutate()}
            disabled={logout.isPending}
            className="inline-flex items-center gap-2 rounded-full border border-white/40 px-5 py-2.5 text-sm font-semibold text-white backdrop-blur transition hover:bg-white/15 disabled:opacity-60"
          >
            <LogOut aria-hidden="true" className="h-4 w-4" />
            {t('auth.signOut')}
          </button>
        }
      >
        <p className="mt-3 text-white/80">
          {profile.maskedEmail} ·{' '}
          {t('account.memberSince', { date: formatMonthYear(profile.memberSince, language) })}
        </p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[20rem_1fr]">
        <ProfileSummary profile={profile} />
        <div className="flex min-w-0 flex-col gap-8">
          <VerificationCard profile={profile} />
          <HostingCard profile={profile} />
          <ProfileFormCard profile={profile} />
          <PasswordCard />
        </div>
      </div>
    </>
  );
}

/** Who you are at a glance, how complete your profile is, and where to go next. */
function ProfileSummary({ profile }: { profile: Profile }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const isHost = profile.roles.includes('Host');

  // The fields a trip host and the safety desk actually rely on.
  const checks = [
    profile.displayName,
    profile.gender,
    profile.phone,
    profile.homeDistrict,
    profile.bio,
    profile.emergencyContactPhone,
  ];
  const done = checks.filter(Boolean).length;
  const percent = Math.round((done / checks.length) * 100);

  const links: { to: string; label: string; icon: LucideIcon }[] = [
    { to: '/me/trips', label: t('nav.myTrips'), icon: Map },
    ...(isHost
      ? [
          { to: '/host/trips', label: t('hosting.myTrips'), icon: Tent },
          { to: '/host/payouts', label: t('payouts.title'), icon: Wallet },
        ]
      : []),
    { to: '/account/verify', label: t('verification.title'), icon: ShieldCheck },
  ];

  return (
    <aside className="flex flex-col gap-6 lg:sticky lg:top-24">
      <div className={`${cardClass} text-center`}>
        <AvatarEditor profile={profile} />
        <p className="mt-4 font-display text-xl font-semibold text-deep">
          {profile.displayName ?? t('account.noName')}
        </p>
        <div className="mt-3 flex flex-wrap justify-center gap-2">
          <VerificationBadge level={profile.verifiedLevel} size="md" />
          {profile.roles
            .filter((role) => role !== 'Traveler')
            .map((role) => (
              <span
                key={role}
                className="rounded-full bg-turmeric/20 px-3 py-1 text-sm font-semibold text-deep"
              >
                {t(`roles.${role}`)}
              </span>
            ))}
        </div>

        <div className="mt-6 text-left">
          <div className="flex items-baseline justify-between text-sm">
            <span className="font-semibold text-deep">{t('account.completeness')}</span>
            <span className="font-semibold text-hill">{formatCount(percent, language)}%</span>
          </div>
          <div
            role="progressbar"
            aria-label={t('account.completeness')}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={percent}
            className="mt-2 h-2 overflow-hidden rounded-full bg-mist"
          >
            <div
              className="h-full rounded-full bg-linear-to-r from-hill to-turmeric transition-all duration-700"
              style={{ width: `${percent}%` }}
            />
          </div>
          {done < checks.length && (
            <p className="mt-2 text-xs text-deep/60">{t('account.completenessHint')}</p>
          )}
        </div>
      </div>

      <nav aria-label={t('account.quickLinks')} className={`${cardClass} p-2! sm:p-2!`}>
        <ul>
          {links.map(({ to, label, icon: Icon }) => (
            <li key={to}>
              <Link
                to={to}
                className="group flex items-center gap-3 rounded-xl px-4 py-3 font-medium text-deep transition hover:bg-mist"
              >
                <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-hill/10 text-hill">
                  <Icon aria-hidden="true" className="h-4.5 w-4.5" />
                </span>
                <span className="flex-1">{label}</span>
                <ChevronRight
                  aria-hidden="true"
                  className="h-4 w-4 text-deep/40 transition group-hover:translate-x-0.5 group-hover:text-hill"
                />
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}

/** A card with an icon in a coloured tile beside its title. */
function SectionCard({
  id,
  icon: Icon,
  title,
  lead,
  tone = 'hill',
  children,
}: {
  id: string;
  icon: LucideIcon;
  title: string;
  lead?: string;
  tone?: 'hill' | 'turmeric';
  children: ReactNode;
}) {
  return (
    <section className={cardClass} aria-labelledby={id}>
      <div className="flex items-start gap-4">
        <span
          className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-xl ${
            tone === 'hill' ? 'bg-hill text-white' : 'bg-turmeric text-night'
          }`}
        >
          <Icon aria-hidden="true" className="h-6 w-6" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 id={id} className="text-xl font-semibold">
            {title}
          </h2>
          {lead && <p className="mt-1 text-sm text-deep/70">{lead}</p>}
        </div>
      </div>
      <div className="mt-6">{children}</div>
    </section>
  );
}

function VerificationCard({ profile }: { profile: Profile }) {
  const { t } = useTranslation();
  const { data: checks } = useMyVerifications();
  const latest = checks?.[0];
  const fullyVerified = profile.verifiedLevel === 'NidSelfie';
  const reached = profile.verifiedLevel ? levels.indexOf(profile.verifiedLevel) : -1;

  return (
    <SectionCard
      id="verification-title"
      icon={ShieldCheck}
      title={t('verification.title')}
      lead={t('verification.why')}
    >
      {/* The three checks as a track: each one passed is filled in. */}
      <ol className="grid gap-3 sm:grid-cols-3">
        {levels.map((level, index) => {
          const passed = index <= reached;
          const next = index === reached + 1;
          return (
            <li
              key={level}
              className={`flex items-center gap-3 rounded-xl p-3 ring-1 ${
                passed
                  ? 'bg-hill/5 ring-hill/20'
                  : next
                    ? 'bg-turmeric/10 ring-turmeric/40'
                    : 'bg-white ring-hill/10'
              }`}
            >
              <span
                className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-bold ${
                  passed
                    ? 'bg-hill text-white'
                    : next
                      ? 'bg-turmeric text-night'
                      : 'bg-mist text-deep/50'
                }`}
              >
                {passed ? <Check aria-hidden="true" className="h-4 w-4" /> : index + 1}
              </span>
              <span
                className={`text-sm leading-snug ${passed ? 'font-semibold text-deep' : 'text-deep/70'}`}
              >
                {t(`verification.levels.${level}`)}
                <span className="sr-only">
                  {' '}
                  ({passed ? t('verification.stepDone') : t('verification.stepTodo')})
                </span>
              </span>
            </li>
          );
        })}
      </ol>

      {latest?.status === 'Pending' && (
        <p role="status" className="mt-4 rounded-xl bg-amber-50 p-4 text-sm text-amber-900">
          {t('verification.pending')}
        </p>
      )}
      {latest?.status === 'Rejected' && (
        <p className="mt-4 rounded-xl bg-jamdani/5 p-4 text-sm text-jamdani">
          {t('verification.rejected')} {latest.reason}
        </p>
      )}

      <div className="mt-5">
        {fullyVerified ? (
          <p className="flex items-center gap-2 text-sm font-semibold text-hill">
            <BadgeCheck aria-hidden="true" className="h-5 w-5" />
            {t('verification.complete')}
          </p>
        ) : (
          latest?.status !== 'Pending' && (
            <Link to="/account/verify" className={primaryButtonClass}>
              {profile.verifiedLevel ? t('verification.upgrade') : t('verification.start')}
            </Link>
          )
        )}
      </div>
    </SectionCard>
  );
}

function HostingCard({ profile }: { profile: Profile }) {
  const { t } = useTranslation();
  const becomeHost = useBecomeHost();
  const isHost = profile.roles.includes('Host');
  const canPublish = isHost && profile.verifiedLevel === 'NidSelfie';

  return (
    <SectionCard
      id="hosting-title"
      icon={Tent}
      tone="turmeric"
      title={t('hosting.title')}
      lead={
        isHost ? (canPublish ? t('hosting.ready') : t('hosting.needsSelfie')) : t('hosting.pitch')
      }
    >
      {!isHost && (
        <>
          <button
            type="button"
            className={primaryButtonClass}
            disabled={becomeHost.isPending}
            onClick={() => becomeHost.mutate()}
          >
            {t('hosting.become')}
          </button>
          {becomeHost.isError && (
            <p role="alert" className="mt-2 text-sm text-jamdani">
              {errorText(becomeHost.error, t)}
            </p>
          )}
        </>
      )}

      {isHost && (
        <div className="flex flex-wrap gap-3">
          <Link to="/host/trips" className={primaryButtonClass}>
            {t('hosting.myTrips')}
          </Link>
          <Link to="/host/payouts" className={secondaryButtonClass}>
            {t('payouts.title')}
          </Link>
        </div>
      )}
    </SectionCard>
  );
}

function ProfileFormCard({ profile }: { profile: Profile }) {
  const { t } = useTranslation();
  const update = useUpdateProfile();
  const [saved, setSaved] = useState(false);
  // Fixed once an identity check has passed; only support can change it.
  const genderLocked = profile.verifiedLevel !== null && profile.gender !== null;

  const {
    register,
    handleSubmit,
    formState: { errors, isDirty },
  } = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      displayName: profile.displayName ?? '',
      gender:
        profile.gender === 'Female' || profile.gender === 'Male' || profile.gender === 'Other'
          ? profile.gender
          : '',
      phone: profile.phone ?? '',
      homeDistrict: profile.homeDistrict ?? '',
      bio: profile.bio ?? '',
      emergencyContactName: profile.emergencyContactName ?? '',
      emergencyContactPhone: profile.emergencyContactPhone ?? '',
    },
  });

  const onSubmit = handleSubmit((form) => {
    setSaved(false);
    update.mutate(
      {
        displayName: form.displayName,
        gender: form.gender === '' ? null : form.gender,
        phone: form.phone || null,
        homeDistrict: form.homeDistrict || null,
        bio: form.bio || null,
        emergencyContactName: form.emergencyContactName || null,
        emergencyContactPhone: form.emergencyContactPhone || null,
      },
      { onSuccess: () => setSaved(true) },
    );
  });

  const fieldError = (message: string | undefined) =>
    message ? t(`profile.invalid.${message}`, { defaultValue: t('errors.invalid') }) : undefined;

  return (
    <SectionCard
      id="profile-title"
      icon={UserRound}
      title={t('profile.title')}
      lead={t('profile.lead')}
    >
      <form onSubmit={(event) => void onSubmit(event)} noValidate className="flex flex-col gap-8">
        <FormGroup icon={UserRound} title={t('profile.sectionPersonal')}>
          <TextField
            id="displayName"
            label={t('profile.name')}
            autoComplete="name"
            error={fieldError(errors.displayName?.message)}
            {...register('displayName')}
          />
          <SelectField
            id="gender"
            label={t('profile.gender')}
            disabled={genderLocked}
            hint={genderLocked ? t('profile.genderLocked') : t('profile.genderHint')}
            {...register('gender')}
          >
            <option value="">{t('profile.genderUnset')}</option>
            {genders.map((gender) => (
              <option key={gender} value={gender ?? ''}>
                {t(`gender.${gender}`)}
              </option>
            ))}
          </SelectField>
          <TextField
            id="phone"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            label={t('profile.phone')}
            hint={t('profile.phoneHint')}
            placeholder="01712345678"
            error={fieldError(errors.phone?.message)}
            {...register('phone')}
          />
          <TextField
            id="homeDistrict"
            label={t('profile.homeDistrict')}
            error={fieldError(errors.homeDistrict?.message)}
            {...register('homeDistrict')}
          />
        </FormGroup>

        <FormGroup icon={NotebookPen} title={t('profile.bio')} single>
          <TextAreaField
            id="bio"
            rows={4}
            label={t('profile.bioLabel')}
            hint={t('profile.bioHint')}
            error={fieldError(errors.bio?.message)}
            {...register('bio')}
          />
        </FormGroup>

        {/* A floated legend sits inside the box instead of on its border, and still names the
            group for screen readers. */}
        <fieldset className="min-w-0 rounded-2xl border border-jamdani/15 bg-jamdani/3 p-5 sm:p-6">
          <legend className="float-left mb-5 flex w-full items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-jamdani text-white">
              <HeartPulse aria-hidden="true" className="h-5 w-5" />
            </span>
            <span>
              <span className="block font-display font-semibold text-deep">
                {t('profile.emergencyTitle')}
              </span>
              <span className="block text-sm text-deep/65">{t('profile.emergencyLead')}</span>
            </span>
          </legend>
          <div className="clear-both grid gap-5 sm:grid-cols-2">
            <TextField
              id="emergencyContactName"
              label={t('profile.emergencyName')}
              error={fieldError(errors.emergencyContactName?.message)}
              {...register('emergencyContactName')}
            />
            <TextField
              id="emergencyContactPhone"
              type="tel"
              inputMode="tel"
              label={t('profile.emergencyPhone')}
              hint={t('profile.emergencyHint')}
              error={fieldError(errors.emergencyContactPhone?.message)}
              {...register('emergencyContactPhone')}
            />
          </div>
        </fieldset>

        <div className="flex flex-wrap items-center gap-4 border-t border-hill/10 pt-6">
          <button type="submit" className={primaryButtonClass} disabled={update.isPending}>
            {update.isPending ? t('common.saving') : t('common.save')}
          </button>
          {isDirty && !update.isPending && !saved && (
            <p className="text-sm text-deep/60">{t('profile.unsaved')}</p>
          )}
          {saved && (
            <p role="status" className="flex items-center gap-1.5 text-sm font-semibold text-hill">
              <Check aria-hidden="true" className="h-4 w-4" />
              {t('profile.saved')}
            </p>
          )}
          {update.isError && (
            <p role="alert" className="text-sm text-jamdani">
              {errorText(update.error, t)}
            </p>
          )}
        </div>
      </form>
    </SectionCard>
  );
}

/** A titled group of fields inside a form: two columns, or one for long text. */
function FormGroup({
  icon: Icon,
  title,
  single = false,
  children,
}: {
  icon: LucideIcon;
  title: string;
  single?: boolean;
  children: ReactNode;
}) {
  return (
    <div>
      <p className="mb-4 flex items-center gap-2 font-display font-semibold text-deep">
        <Icon aria-hidden="true" className="h-5 w-5 text-hill" />
        {title}
      </p>
      <div className={single ? '' : 'grid gap-5 sm:grid-cols-2'}>{children}</div>
    </div>
  );
}

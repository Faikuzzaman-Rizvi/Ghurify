import { useEffect, useMemo, useRef, useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowLeft,
  BadgeCheck,
  Camera,
  CircleAlert,
  CircleCheck,
  Clock,
  Fingerprint,
  LockKeyhole,
  ShieldCheck,
  Trash2,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  cardClass,
  DateField,
  primaryButtonClass,
  SelectField,
  TextField,
} from '@/components/Field';
import { PageBanner } from '@/components/ui/PageBanner';
import { VerificationBadge } from '@/components/VerificationBadge';
import { errorText } from '@/lib/errors';
import { todayInDhaka } from '@/lib/format';
import { acceptedImageTypes } from '@/lib/images';
import type { IdDocumentType, MyDocumentView, VerificationDocumentKind } from './profileApi';
import {
  useMyDocuments,
  useMyProfile,
  useRemoveDocument,
  useStartVerification,
  useUploadDocument,
} from './useProfile';

const idTypes: readonly IdDocumentType[] = ['Nid', 'Passport', 'DrivingLicence'];

/** The photos of the ID itself, by type. Mirrors VerificationRequirements on the server. */
const idPhotos: Record<IdDocumentType, readonly VerificationDocumentKind[]> = {
  Nid: ['NidFront', 'NidBack'],
  Passport: ['PassportPhotoPage'],
  DrivingLicence: ['DrivingLicenceFront', 'DrivingLicenceBack'],
};

/** What the typed number must look like, per ID. The server checks again. */
function numberIsValid(type: IdDocumentType, value: string): boolean {
  const compact = value.replace(/[\s-/]/g, '');
  if (type === 'Nid') return /^\d+$/.test(compact) && [10, 13, 17].includes(compact.length);
  if (!/^[A-Za-z0-9]+$/.test(compact) || !/\d/.test(compact)) return false;
  return type === 'Passport'
    ? compact.length >= 7 && compact.length <= 12
    : compact.length >= 8 && compact.length <= 20;
}

const schema = z
  .object({
    level: z.enum(['Nid', 'NidSelfie']),
    idType: z.enum(['Nid', 'Passport', 'DrivingLicence']),
    idNumber: z.string().trim(),
    dateOfBirth: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'dob'),
  })
  .refine((form) => numberIsValid(form.idType, form.idNumber), {
    path: ['idNumber'],
    message: 'number',
  });

type VerificationForm = z.infer<typeof schema>;

/**
 * Asks for an identity check: which ID, its number and date of birth, and photos of it (both
 * sides, or the passport photo page), plus a selfie holding it for the host level. The number
 * goes to the server once and is never shown again; the photos are kept privately and deleted
 * 30 days after the decision.
 */
export function VerificationPage() {
  const { t } = useTranslation();
  const { data: profile } = useMyProfile();
  const { data: uploaded = [] } = useMyDocuments();
  const start = useStartVerification();

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<VerificationForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      level: profile?.roles.includes('Host') ? 'NidSelfie' : 'Nid',
      idType: 'Nid',
      idNumber: '',
      dateOfBirth: '',
    },
  });

  const level = useWatch({ control, name: 'level' });
  const idType = useWatch({ control, name: 'idType' });
  const idNumberField = register('idNumber');

  const needed = useMemo<VerificationDocumentKind[]>(
    () => [...idPhotos[idType], ...(level === 'NidSelfie' ? (['Selfie'] as const) : [])],
    [idType, level],
  );

  // The newest ready photo of each kind; earlier ones of the same kind are replaced.
  const ready = useMemo(() => {
    const byKind = new Map<VerificationDocumentKind, MyDocumentView>();
    for (const document of uploaded) {
      if (document.status === 'Ready') byKind.set(document.kind, document);
    }
    return byKind;
  }, [uploaded]);

  const missing = needed.filter((kind) => !ready.has(kind));
  const licence = ready.get('ProfessionalLicence');

  const onSubmit = handleSubmit((form) => {
    if (missing.length > 0) return;
    const documentIds = [
      ...needed.map((kind) => ready.get(kind)!),
      ...(licence ? [licence] : []),
    ].map((document) => asNumber(document.id));

    start.mutate(
      { ...form, documentIds },
      // Clear the number from the form as soon as it has been sent.
      { onSettled: () => reset({ ...form, idNumber: '' }) },
    );
  });

  const result = start.data;
  // Nothing here can be sent without a name and a gender: the server refuses the check with
  // profile_incomplete. Say so once, and turn the form off until the profile is filled in,
  // rather than let somebody type an ID number and a date of birth for nothing.
  const missingProfile = Boolean(profile && (!profile.displayName || !profile.gender));

  return (
    <>
      <PageBanner
        compact
        slug="bandarban"
        kind="Hills"
        eyebrow={
          <Link to="/account" className="inline-flex items-center gap-1.5 hover:text-white">
            <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
            {t('account.title')}
          </Link>
        }
        title={t('verification.title')}
      >
        <p className="mt-3 max-w-xl text-white/80">{t('verification.intro')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[1fr_20rem]">
        {result ? (
          <section
            className={`${cardClass} flex flex-col items-center gap-4 text-center`}
            role="status"
          >
            {result.status === 'Approved' && (
              <>
                <span className="flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">
                  <BadgeCheck aria-hidden="true" className="h-8 w-8" />
                </span>
                <p className="text-xl font-semibold text-hill">{t('verification.approved')}</p>
                <VerificationBadge level={result.level} size="md" />
              </>
            )}
            {result.status === 'Pending' && (
              <>
                <span className="flex h-16 w-16 items-center justify-center rounded-full bg-turmeric/20 text-ochre">
                  <Clock aria-hidden="true" className="h-8 w-8" />
                </span>
                <p className="text-lg font-semibold text-deep">{t('verification.pending')}</p>
              </>
            )}
            {result.status === 'Rejected' && (
              <>
                <span className="flex h-16 w-16 items-center justify-center rounded-full bg-jamdani/10 text-jamdani">
                  <CircleAlert aria-hidden="true" className="h-8 w-8" />
                </span>
                <p className="text-lg font-semibold text-jamdani">
                  {t('verification.rejected')} {result.reason}
                </p>
              </>
            )}
            <Link to="/account" className={`${primaryButtonClass} mt-2`}>
              {t('verification.done')}
            </Link>
          </section>
        ) : (
          <form
            onSubmit={(event) => void onSubmit(event)}
            noValidate
            className={`${cardClass} flex flex-col gap-5`}
          >
            <div className="flex items-center gap-4 border-b border-hill/10 pb-5">
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-hill text-white">
                <Fingerprint aria-hidden="true" className="h-6 w-6" />
              </span>
              <div>
                <h2 className="text-xl font-semibold">{t('verification.formTitle')}</h2>
                <p className="text-sm text-deep/70">{t('verification.formLead')}</p>
              </div>
            </div>

            {missingProfile && (
              <p role="note" className="rounded-xl bg-amber-50 p-4 text-sm text-amber-900">
                {t('verification.completeProfileFirst')}{' '}
                <Link to="/account" className="font-semibold underline">
                  {t('account.title')}
                </Link>
              </p>
            )}

            <fieldset disabled={missingProfile} className="min-w-0 border-0 p-0">
              <div className="grid gap-5 sm:grid-cols-2">
                <Controller
                  control={control}
                  name="level"
                  render={({ field }) => (
                    <SelectField
                      id="level"
                      label={t('verification.level')}
                      hint={t('verification.levelHint')}
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.value)}
                      onBlur={field.onBlur}
                    >
                      <option value="Nid">{t('verification.levelOptions.Nid')}</option>
                      <option value="NidSelfie">{t('verification.levelOptions.NidSelfie')}</option>
                    </SelectField>
                  )}
                />
                <Controller
                  control={control}
                  name="idType"
                  render={({ field }) => (
                    <SelectField
                      id="idType"
                      label={t('verification.idType')}
                      hint={t('verification.idTypeHint')}
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.value)}
                      onBlur={field.onBlur}
                    >
                      {idTypes.map((type) => (
                        <option key={type} value={type}>
                          {t(`verification.idTypes.${type}`)}
                        </option>
                      ))}
                    </SelectField>
                  )}
                />
                <TextField
                  id="idNumber"
                  inputMode={idType === 'Nid' ? 'numeric' : 'text'}
                  // A national ID number is digits only, so letters never reach the field.
                  pattern={idType === 'Nid' ? '[0-9 -]*' : undefined}
                  autoComplete="off"
                  label={t(`verification.number.${idType}`)}
                  hint={t(`verification.numberHint.${idType}`)}
                  error={errors.idNumber ? t(`verification.numberInvalid.${idType}`) : undefined}
                  {...idNumberField}
                  onChange={(event) => {
                    if (idType === 'Nid') {
                      const digitsOnly = event.target.value.replace(/[^\d\s-]/g, '');
                      if (digitsOnly !== event.target.value) event.target.value = digitsOnly;
                    }
                    void idNumberField.onChange(event);
                  }}
                />
                <Controller
                  control={control}
                  name="dateOfBirth"
                  render={({ field }) => (
                    <DateField
                      id="dateOfBirth"
                      max={todayInDhaka()}
                      placeholder={t('datePicker.dobPlaceholder')}
                      label={t('verification.dob')}
                      error={errors.dateOfBirth ? t('verification.dobInvalid') : undefined}
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                    />
                  )}
                />
              </div>
            </fieldset>

            <fieldset disabled={missingProfile} className="flex flex-col gap-3">
              <legend className="mb-1 font-semibold text-deep">
                {t('verification.photos.title')}
              </legend>
              <p className="text-sm text-deep/70">{t('verification.photos.lead')}</p>
              <div className="grid gap-3 sm:grid-cols-2">
                {needed.map((kind) => (
                  <PhotoSlot key={kind} kind={kind} document={ready.get(kind)} required />
                ))}
                <PhotoSlot kind="ProfessionalLicence" document={licence} />
              </div>
            </fieldset>

            {start.isError && (
              <p role="alert" className="text-sm text-jamdani">
                {errorText(start.error, t)}
              </p>
            )}
            <button
              type="submit"
              className={`${primaryButtonClass} self-start`}
              disabled={start.isPending || missing.length > 0 || missingProfile}
            >
              <ShieldCheck aria-hidden="true" className="h-4 w-4" />
              {start.isPending ? t('verification.checking') : t('verification.submit')}
            </button>
            {missing.length > 0 && (
              <p className="text-sm text-deep/70">
                {t('verification.photos.stillNeeded', {
                  list: missing.map((kind) => t(`verification.kinds.${kind}`)).join(', '),
                })}
              </p>
            )}
          </form>
        )}

        <aside className="flex flex-col gap-6 lg:sticky lg:top-24">
          <div className={cardClass}>
            <h2 className="flex items-center gap-2 text-lg font-semibold">
              <ShieldCheck aria-hidden="true" className="h-5 w-5 text-hill" />
              {t('verification.whyTitle')}
            </h2>
            <p className="mt-3 text-sm leading-relaxed text-deep/75">{t('verification.why')}</p>
          </div>
          <div className={`${cardClass} bg-hill! text-white`}>
            <h2 className="flex items-center gap-2 text-lg font-semibold text-white!">
              <LockKeyhole aria-hidden="true" className="h-5 w-5 text-dusk" />
              {t('verification.privacyTitle')}
            </h2>
            <p className="mt-3 text-sm leading-relaxed text-white/85">
              {t('verification.privacy')}
            </p>
          </div>
        </aside>
      </div>
    </>
  );
}

/**
 * One photo to take or choose: a preview of what was picked, upload progress, and whether the
 * server accepted it. On a phone, "Take photo" opens the camera (the selfie the front one).
 */
function PhotoSlot({
  kind,
  document,
  required = false,
}: {
  kind: VerificationDocumentKind;
  document: MyDocumentView | undefined;
  required?: boolean;
}) {
  const { t } = useTranslation();
  const upload = useUploadDocument();
  const remove = useRemoveDocument();
  const input = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [progress, setProgress] = useState(0);

  useEffect(
    () => () => {
      if (preview) URL.revokeObjectURL(preview);
    },
    [preview],
  );

  const id = `photo-${kind}`;
  const done = document !== undefined;

  return (
    <div
      className={`flex flex-col gap-2 rounded-2xl border-2 p-3 ${
        done ? 'border-emerald-300 bg-emerald-50/50' : 'border-dashed border-hill/25'
      }`}
    >
      <div className="flex items-center justify-between gap-2">
        <label htmlFor={id} className="text-sm font-semibold text-deep">
          {t(`verification.kinds.${kind}`)}
          {!required && (
            <span className="font-normal text-deep/60"> · {t('verification.photos.optional')}</span>
          )}
        </label>
        {done && (
          <CircleCheck
            aria-label={t('verification.photos.accepted')}
            className="h-5 w-5 text-emerald-600"
          />
        )}
      </div>
      <p className="text-xs text-deep/60">{t(`verification.kindHints.${kind}`)}</p>

      {preview && <img src={preview} alt="" className="h-28 w-full rounded-xl object-cover" />}

      <input
        ref={input}
        id={id}
        type="file"
        accept={acceptedImageTypes}
        capture={kind === 'Selfie' ? 'user' : 'environment'}
        className="sr-only"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = '';
          if (!file) return;
          setPreview(URL.createObjectURL(file));
          setProgress(0);
          upload.mutate({ kind, file, onProgress: setProgress });
        }}
      />

      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => input.current?.click()}
          disabled={upload.isPending}
          className="inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1.5 text-sm font-semibold text-hill ring-1 ring-hill/30 hover:bg-hill/5 disabled:opacity-60"
        >
          <Camera aria-hidden="true" className="h-4 w-4" />
          {done ? t('verification.photos.replace') : t('verification.photos.add')}
        </button>
        {done && (
          <button
            type="button"
            onClick={() => {
              setPreview(null);
              remove.mutate(asNumber(document.id));
            }}
            disabled={remove.isPending}
            aria-label={t('verification.photos.remove', { kind: t(`verification.kinds.${kind}`) })}
            className="inline-flex items-center rounded-full p-1.5 text-deep/60 hover:bg-jamdani/10 hover:text-jamdani"
          >
            <Trash2 aria-hidden="true" className="h-4 w-4" />
          </button>
        )}
        {upload.isPending && (
          <span role="status" className="text-xs text-deep/70">
            {t('verification.photos.uploading', { percent: Math.round(progress * 100) })}
          </span>
        )}
      </div>

      {upload.isError && (
        <p role="alert" className="text-xs text-jamdani">
          {errorText(upload.error, t)}
        </p>
      )}
    </div>
  );
}

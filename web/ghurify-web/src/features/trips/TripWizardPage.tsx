import { useEffect, useMemo, useState } from 'react';
import {
  useFieldArray,
  useForm,
  useWatch,
  type Control,
  type FieldErrors,
  type UseFormRegister,
} from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  CalendarDays,
  Check,
  Eye,
  MapPin,
  Plus,
  ReceiptText,
  Rocket,
  Trash2,
  TriangleAlert,
  Users,
  type LucideIcon,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  accentButtonClass,
  cardClass,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextAreaField,
  TextField,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { PageBanner } from '@/components/ui/PageBanner';
import { Photo } from '@/components/ui/Photo';
import { useMyProfile } from '@/features/auth/useProfile';
import { errorText } from '@/lib/errors';
import {
  formatCount,
  formatDateRange,
  formatMoney,
  toLanguage,
  todayInDhaka,
  tripDays,
} from '@/lib/format';
import type { SaveTripCommand } from './hostApi';
import {
  groupTypes,
  type CostCategory,
  type DestinationSummary,
  type Difficulty,
  type TripDetail,
} from './tripsApi';
import { useCreateTrip, usePublishTrip, useUpdateTrip } from './useHostTrips';
import { useDestinations, useTrip } from './useTrips';

const costCategories = ['Transport', 'Stay', 'Food', 'Fees', 'Guide', 'Buffer'] as const;
const difficulties = ['Easy', 'Moderate', 'Challenging'] as const;
const steps = ['basics', 'costs', 'itinerary', 'review'] as const;
type Step = (typeof steps)[number];

const isoDate = /^\d{4}-\d{2}-\d{2}$/;

/** Mirrors SaveTripCommandValidator and the TripPlan save rules on the server. */
const tripSchema = z
  .object({
    destinationSlug: z.string().min(1, 'destination'),
    title: z.string().trim().min(5, 'title').max(150, 'title'),
    summary: z.string().trim().min(20, 'summary').max(1000, 'summary'),
    startDate: z.string().regex(isoDate, 'date'),
    endDate: z.string().regex(isoDate, 'date'),
    meetingPoint: z.string().trim().min(3, 'meetingPoint').max(200, 'meetingPoint'),
    seats: z.number('seats').int('seats').min(1, 'seats').max(50, 'seats'),
    groupType: z.enum(['Open', 'WomenOnly', 'Students', 'Families']),
    costItems: z
      .array(
        z.object({
          category: z.enum(costCategories),
          description: z.string().trim().max(150, 'description'),
          amount: z.number('amount').min(0, 'amount').max(1_000_000, 'amount'),
        }),
      )
      .min(1, 'costs')
      .max(20, 'costs'),
    itinerary: z.array(
      z.object({
        dayNo: z.number(),
        title: z.string().trim().min(1, 'dayTitle').max(150, 'dayTitle'),
        details: z.string().trim().min(1, 'dayDetails').max(1000, 'dayDetails'),
        difficulty: z.enum(difficulties),
      }),
    ),
  })
  .refine((trip) => trip.startDate > todayInDhaka(), { path: ['startDate'], message: 'future' })
  .refine((trip) => trip.endDate >= trip.startDate, {
    path: ['endDate'],
    message: 'endBeforeStart',
  })
  .refine((trip) => tripDays(trip.startDate, trip.endDate) <= 30, {
    path: ['endDate'],
    message: 'tooLong',
  });

type TripForm = z.infer<typeof tripSchema>;

const stepFields: Record<Step, (keyof TripForm)[]> = {
  basics: [
    'destinationSlug',
    'title',
    'summary',
    'startDate',
    'endDate',
    'meetingPoint',
    'seats',
    'groupType',
  ],
  costs: ['costItems'],
  itinerary: ['itinerary'],
  review: [],
};

const blankTrip: TripForm = {
  destinationSlug: '',
  title: '',
  summary: '',
  startDate: '',
  endDate: '',
  meetingPoint: '',
  seats: 12,
  groupType: 'Open',
  costItems: [
    { category: 'Transport', description: '', amount: 0 },
    { category: 'Stay', description: '', amount: 0 },
    { category: 'Food', description: '', amount: 0 },
  ],
  itinerary: [],
};

function fromTrip(trip: TripDetail): TripForm {
  return {
    destinationSlug: trip.destination.slug,
    title: trip.title,
    summary: trip.summary,
    startDate: trip.startDate,
    endDate: trip.endDate,
    meetingPoint: trip.meetingPoint,
    seats: asNumber(trip.seats),
    groupType: trip.groupType,
    costItems: trip.costItems.map((item) => ({
      category: item.category,
      description: item.description ?? '',
      amount: asNumber(item.amount),
    })),
    itinerary: trip.itinerary.map((day) => ({
      dayNo: asNumber(day.dayNo),
      title: day.title,
      details: day.details,
      difficulty: day.difficulty,
    })),
  };
}

function toCommand(form: TripForm): SaveTripCommand {
  return {
    ...form,
    // The price is the lines, always: computed here and checked again by the server.
    pricePerPerson: form.costItems.reduce((sum, item) => sum + item.amount, 0),
    costItems: form.costItems.map((item) => ({
      category: item.category,
      description: item.description || null,
      amount: item.amount,
    })),
    itinerary: [...form.itinerary].sort((a, b) => a.dayNo - b.dayNo),
  };
}

/** Create a trip (/host/trips/new) or edit one of your own (/host/trips/:id/edit). */
export function TripWizardPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const tripId = id ? Number(id) : null;
  const trip = useTrip(tripId ?? 0);

  if (tripId === null) {
    return <TripWizard initial={blankTrip} tripId={null} status="Draft" />;
  }

  if (trip.isPending) {
    return (
      <div role="status" className="container-page max-w-3xl py-12">
        <span className="sr-only">{t('common.loading')}</span>
        <div aria-hidden="true" className="h-96 animate-pulse rounded-3xl bg-hill/10" />
      </div>
    );
  }

  if (trip.isError) {
    return (
      <div className="container-page max-w-3xl py-12">
        <ErrorState message={errorText(trip.error, t)} onRetry={() => void trip.refetch()} />
      </div>
    );
  }

  return <TripWizard initial={fromTrip(trip.data)} tripId={tripId} status={trip.data.status} />;
}

function TripWizard({
  initial,
  tripId,
  status,
}: {
  initial: TripForm;
  tripId: number | null;
  status: TripDetail['status'];
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const navigate = useNavigate();
  const [step, setStep] = useState<Step>('basics');
  const { data: profile } = useMyProfile();
  const { data: destinations } = useDestinations();
  const create = useCreateTrip();
  const update = useUpdateTrip(tripId ?? 0);
  const publish = usePublishTrip();
  const canPublish = profile?.roles.includes('Host') && profile.verifiedLevel === 'NidSelfie';

  const form = useForm<TripForm>({ resolver: zodResolver(tripSchema), defaultValues: initial });
  const {
    register,
    control,
    trigger,
    handleSubmit,
    formState: { errors },
  } = form;

  const startDate = useWatch({ control, name: 'startDate' });
  const endDate = useWatch({ control, name: 'endDate' });
  const costItems = useWatch({ control, name: 'costItems' });
  const destinationSlug = useWatch({ control, name: 'destinationSlug' });
  const total = (costItems ?? []).reduce((sum, item) => sum + (Number(item.amount) || 0), 0);
  const destination = destinations?.find((item) => item.slug === destinationSlug);

  const days = useFieldArray({ control, name: 'itinerary' });

  // One itinerary entry per day of the trip, kept in step with the dates; a day already written
  // keeps its text when the dates move.
  const dayCount =
    isoDate.test(startDate) && isoDate.test(endDate) && endDate >= startDate
      ? Math.min(tripDays(startDate, endDate), 30)
      : 0;
  const { fields: dayFields, replace: replaceDays } = days;

  useEffect(() => {
    if (dayCount === 0 || dayFields.length === dayCount) {
      return;
    }

    const current = form.getValues('itinerary');
    replaceDays(
      Array.from(
        { length: dayCount },
        (_, index) =>
          current.find((day) => day.dayNo === index + 1) ?? {
            dayNo: index + 1,
            title: '',
            details: '',
            difficulty: 'Easy' as const,
          },
      ),
    );
  }, [dayCount, dayFields.length, form, replaceDays]);

  const saving = create.isPending || update.isPending || publish.isPending;
  const saveError = create.error ?? update.error ?? publish.error;
  const stepIndex = steps.indexOf(step);

  async function goTo(next: Step) {
    const currentIndex = steps.indexOf(step);
    const nextIndex = steps.indexOf(next);

    // Moving forward checks the steps being left behind; going back never blocks.
    if (nextIndex > currentIndex) {
      for (const pending of steps.slice(currentIndex, nextIndex)) {
        if (!(await trigger(stepFields[pending]))) {
          setStep(pending);
          return;
        }
      }
    }

    setStep(next);
  }

  const save = (andPublish: boolean) =>
    handleSubmit(async (values) => {
      const command = toCommand(values);
      const id = tripId ?? asNumber((await create.mutateAsync(command)).id);

      if (tripId !== null) {
        await update.mutateAsync(command);
      }

      if (andPublish && status === 'Draft') {
        await publish.mutateAsync(id);
      }

      void navigate('/host/trips', { state: { saved: id } });
    });

  return (
    <>
      <PageBanner
        compact
        slug={destination?.slug ?? 'bandarban'}
        kind={destination?.kind ?? 'Hills'}
        eyebrow={
          <Link to="/host/trips" className="inline-flex items-center gap-1.5 hover:text-white">
            <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
            {t('wizard.backToTrips')}
          </Link>
        }
        title={tripId ? t('wizard.editTitle') : t('wizard.newTitle')}
      >
        <p className="mt-3 max-w-xl text-white/80">{t('wizard.lead')}</p>
      </PageBanner>

      <div className="container-page relative z-10 -mt-14 grid items-start gap-8 pb-8 lg:grid-cols-[1fr_21rem]">
        <div className="flex min-w-0 flex-col gap-6">
          <nav aria-label={t('wizard.steps')} className={`${cardClass} p-4! sm:p-5!`}>
            <ol className="flex items-center">
              {steps.map((name, index) => {
                const done = index < stepIndex;
                const current = index === stepIndex;
                return (
                  <li key={name} className="flex flex-1 items-center last:flex-none">
                    <button
                      type="button"
                      onClick={() => void goTo(name)}
                      aria-current={current ? 'step' : undefined}
                      aria-label={`${index + 1}. ${t(`wizard.step.${name}`)}`}
                      className="group flex items-center gap-2.5 rounded-full p-1 pr-2"
                    >
                      <span
                        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-bold transition ${
                          current
                            ? 'bg-hill text-white ring-4 ring-hill/15'
                            : done
                              ? 'bg-turmeric text-night'
                              : 'bg-mist text-deep/50 group-hover:bg-hill/10'
                        }`}
                      >
                        {done ? <Check aria-hidden="true" className="h-4 w-4" /> : index + 1}
                      </span>
                      <span
                        className={`hidden text-sm font-semibold sm:block ${
                          current ? 'text-deep' : 'text-deep/60'
                        }`}
                      >
                        {t(`wizard.step.${name}`)}
                      </span>
                    </button>
                    {index < steps.length - 1 && (
                      <span
                        aria-hidden="true"
                        className={`mx-2 h-0.5 flex-1 rounded-full ${done ? 'bg-turmeric' : 'bg-hill/10'}`}
                      />
                    )}
                  </li>
                );
              })}
            </ol>
          </nav>

          <form
            noValidate
            onSubmit={(event) => event.preventDefault()}
            className={`${cardClass} flex flex-col gap-6`}
          >
            <header className="border-b border-hill/10 pb-5">
              <p className="eyebrow">
                {t('wizard.stepOf', { n: stepIndex + 1, total: steps.length })}
              </p>
              <h2 className="mt-2 text-2xl font-semibold">{t(`wizard.step.${step}`)}</h2>
              <p className="mt-1 text-sm text-deep/70">{t(`wizard.stepLead.${step}`)}</p>
            </header>

            {step === 'basics' && <BasicsStep register={register} errors={errors} />}
            {step === 'costs' && (
              <CostsStep register={register} control={control} errors={errors} total={total} />
            )}
            {step === 'itinerary' && (
              <ItineraryStep register={register} errors={errors} days={days.fields} />
            )}
            {step === 'review' && (
              <ReviewStep
                values={form.getValues()}
                total={total}
                language={language}
                canPublish={Boolean(canPublish)}
                status={status}
              />
            )}

            {saveError && (
              <p role="alert" className="rounded-xl bg-jamdani/5 p-4 text-sm text-jamdani">
                {errorText(saveError, t)}
              </p>
            )}

            <div className="flex flex-wrap justify-between gap-3 border-t border-hill/10 pt-5">
              <button
                type="button"
                className={secondaryButtonClass}
                disabled={step === 'basics'}
                onClick={() => void goTo(steps[stepIndex - 1] ?? 'basics')}
              >
                ← {t('common.previous')}
              </button>

              {step !== 'review' ? (
                <button
                  type="button"
                  className={primaryButtonClass}
                  onClick={() => void goTo(steps[stepIndex + 1] ?? 'review')}
                >
                  {t('common.next')} →
                </button>
              ) : (
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    className={secondaryButtonClass}
                    disabled={saving}
                    onClick={() => void save(false)()}
                  >
                    {status === 'Draft' ? t('wizard.saveDraft') : t('wizard.saveChanges')}
                  </button>
                  {status === 'Draft' && (
                    <button
                      type="button"
                      className={accentButtonClass}
                      disabled={saving || !canPublish}
                      onClick={() => void save(true)()}
                    >
                      <Rocket aria-hidden="true" className="h-4 w-4" />
                      {t('wizard.publish')}
                    </button>
                  )}
                </div>
              )}
            </div>
          </form>
        </div>

        <LivePreview control={control} total={total} destination={destination} />
      </div>
    </>
  );
}

/**
 * The trip as travellers will see its card, updating as the host types. Hidden from screen
 * readers: everything in it is already in the form.
 */
function LivePreview({
  control,
  total,
  destination,
}: {
  control: Control<TripForm>;
  total: number;
  destination: DestinationSummary | undefined;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [title, startDate, endDate, seats, groupType] = useWatch({
    control,
    name: ['title', 'startDate', 'endDate', 'seats', 'groupType'],
  });
  const hasDates = isoDate.test(startDate) && isoDate.test(endDate) && endDate >= startDate;

  return (
    <aside aria-hidden="true" className="hidden lg:sticky lg:top-24 lg:block">
      <div className="relative isolate flex aspect-4/5 flex-col justify-end overflow-hidden rounded-2xl bg-night text-white shadow-xl">
        <Photo
          slug={destination?.slug ?? 'bandarban'}
          kind={destination?.kind ?? 'Hills'}
          cut="card"
          decorative
          className="absolute! inset-0 -z-10"
        />
        <div className="absolute inset-0 -z-10 bg-linear-to-t from-night via-night/40 to-transparent" />
        <span className="absolute left-4 top-4 rounded-full bg-white/90 px-3 py-1 text-xs font-semibold text-deep">
          {t(`groupType.${groupType}`)}
        </span>
        <span className="absolute right-4 top-0 rotate-180 rounded-t-md bg-turmeric px-2 py-3 font-display text-sm font-bold text-night [writing-mode:vertical-rl]">
          {formatMoney(total, language)}
        </span>
        <div className="p-5">
          <p className="flex flex-wrap gap-x-3 text-xs text-white/85">
            <span className="inline-flex items-center gap-1">
              <MapPin className="h-3.5 w-3.5 text-dusk" />
              {destination
                ? language === 'bn'
                  ? destination.nameBn
                  : destination.name
                : t('wizard.previewNoPlace')}
            </span>
            {hasDates && (
              <span className="inline-flex items-center gap-1">
                <CalendarDays className="h-3.5 w-3.5 text-dusk" />
                {formatDateRange(startDate, endDate, language)}
              </span>
            )}
          </p>
          <p className="mt-2 font-display text-xl font-semibold leading-snug">
            {title.trim() || t('wizard.previewNoTitle')}
          </p>
          <span className="mt-3 block h-px w-12 bg-dusk" />
          <p className="mt-3 text-sm text-white/80">
            {Number.isFinite(seats) && seats > 0
              ? t('trips.seatsLeft', { count: seats, n: formatCount(seats, language) })
              : ''}
          </p>
        </div>
      </div>
      <p className="mt-4 flex gap-2 text-xs leading-relaxed text-deep/60">
        <Eye className="h-4 w-4 shrink-0 text-hill" />
        <span>
          <span className="font-semibold text-deep">{t('wizard.preview')}.</span>{' '}
          {t('wizard.previewHint')}
        </span>
      </p>
    </aside>
  );
}

interface StepProps {
  register: UseFormRegister<TripForm>;
  errors: FieldErrors<TripForm>;
}

/** Translates a schema message key into the reader's language. */
function useFieldError() {
  const { t } = useTranslation();
  return (message: string | undefined) =>
    message ? t(`wizard.invalid.${message}`, { defaultValue: t('errors.invalid') }) : undefined;
}

function BasicsStep({ register, errors }: StepProps) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data: destinations } = useDestinations();
  const fieldError = useFieldError();

  return (
    <div className="grid gap-5 sm:grid-cols-2">
      <SelectField
        id="destinationSlug"
        label={t('wizard.destination')}
        error={fieldError(errors.destinationSlug?.message)}
        {...register('destinationSlug')}
      >
        <option value="">{t('wizard.chooseDestination')}</option>
        {(destinations ?? []).map((destination) => (
          <option
            key={destination.slug}
            value={destination.slug}
            disabled={destination.status === 'Closed'}
          >
            {language === 'bn' ? destination.nameBn : destination.name}
            {destination.status === 'Closed' ? ` (${t('destination.status.Closed')})` : ''}
          </option>
        ))}
      </SelectField>
      <SelectField
        id="groupType"
        label={t('wizard.groupType')}
        hint={t('wizard.groupTypeHint')}
        {...register('groupType')}
      >
        {groupTypes.map((groupType) => (
          <option key={groupType} value={groupType}>
            {t(`groupType.${groupType}`)}
          </option>
        ))}
      </SelectField>
      <div className="sm:col-span-2">
        <TextField
          id="title"
          label={t('wizard.title')}
          error={fieldError(errors.title?.message)}
          {...register('title')}
        />
      </div>
      <div className="sm:col-span-2">
        <TextAreaField
          id="summary"
          rows={4}
          label={t('wizard.summary')}
          hint={t('wizard.summaryHint')}
          error={fieldError(errors.summary?.message)}
          {...register('summary')}
        />
      </div>
      <TextField
        id="startDate"
        type="date"
        min={todayInDhaka()}
        label={t('wizard.startDate')}
        error={fieldError(errors.startDate?.message)}
        {...register('startDate')}
      />
      <TextField
        id="endDate"
        type="date"
        min={todayInDhaka()}
        label={t('wizard.endDate')}
        error={fieldError(errors.endDate?.message)}
        {...register('endDate')}
      />
      <TextField
        id="meetingPoint"
        label={t('wizard.meetingPoint')}
        error={fieldError(errors.meetingPoint?.message)}
        {...register('meetingPoint')}
      />
      <TextField
        id="seats"
        type="number"
        min={1}
        max={50}
        inputMode="numeric"
        label={t('wizard.seats')}
        error={fieldError(errors.seats?.message)}
        {...register('seats', { valueAsNumber: true })}
      />
    </div>
  );
}

function CostsStep({
  register,
  control,
  errors,
  total,
}: StepProps & { control: Control<TripForm>; total: number }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { fields, append, remove } = useFieldArray({ control, name: 'costItems' });
  const fieldError = useFieldError();

  return (
    <>
      <p className="flex gap-2 rounded-xl bg-mist p-4 text-sm text-deep/80">
        <ReceiptText aria-hidden="true" className="h-5 w-5 shrink-0 text-hill" />
        {t('wizard.costsHint')}
      </p>
      <ul className="flex flex-col gap-3">
        {fields.map((field, index) => (
          <li
            key={field.id}
            className="grid gap-3 rounded-xl p-4 ring-1 ring-hill/10 transition focus-within:ring-hill/30 sm:grid-cols-[1fr_1.5fr_1fr_auto] sm:items-end"
          >
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor={`cost-${index}-category`}
                className="text-xs font-semibold text-deep/70"
              >
                {t('wizard.category')}
              </label>
              <select
                id={`cost-${index}-category`}
                className={`${inputClass} cursor-pointer`}
                {...register(`costItems.${index}.category`)}
              >
                {costCategories.map((category: CostCategory) => (
                  <option key={category} value={category}>
                    {t(`cost.${category}`)}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor={`cost-${index}-description`}
                className="text-xs font-semibold text-deep/70"
              >
                {t('wizard.costDescription')}
              </label>
              <input
                id={`cost-${index}-description`}
                className={inputClass}
                {...register(`costItems.${index}.description`)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor={`cost-${index}-amount`}
                className="text-xs font-semibold text-deep/70"
              >
                {t('wizard.amount')}
              </label>
              <input
                id={`cost-${index}-amount`}
                type="number"
                min={0}
                step={1}
                inputMode="decimal"
                aria-invalid={errors.costItems?.[index]?.amount ? 'true' : 'false'}
                className={`${inputClass} text-right font-semibold`}
                {...register(`costItems.${index}.amount`, { valueAsNumber: true })}
              />
            </div>
            <button
              type="button"
              className="flex h-12 w-12 items-center justify-center self-end rounded-xl text-deep/50 transition hover:bg-jamdani/10 hover:text-jamdani disabled:pointer-events-none disabled:opacity-30"
              onClick={() => remove(index)}
              disabled={fields.length <= 1}
              aria-label={t('wizard.removeLine', { n: index + 1 })}
            >
              <Trash2 aria-hidden="true" className="h-5 w-5" />
            </button>
            {errors.costItems?.[index]?.amount && (
              <p role="alert" className="text-sm text-jamdani sm:col-span-4">
                {fieldError(errors.costItems[index].amount.message)}
              </p>
            )}
          </li>
        ))}
      </ul>
      {errors.costItems?.root?.message && (
        <p role="alert" className="text-sm text-jamdani">
          {fieldError(errors.costItems.root.message)}
        </p>
      )}
      <button
        type="button"
        className="flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-hill/20 py-3 font-semibold text-hill transition hover:border-hill/50 hover:bg-mist disabled:opacity-40"
        disabled={fields.length >= 20}
        onClick={() => append({ category: 'Buffer', description: '', amount: 0 })}
      >
        <Plus aria-hidden="true" className="h-4 w-4" />
        {t('wizard.addLine')}
      </button>
      <p
        className="flex items-baseline justify-between rounded-xl bg-hill px-5 py-4 font-display text-lg font-semibold text-white"
        aria-live="polite"
      >
        <span>{t('trip.total')}</span>
        <span className="text-2xl font-bold">{formatMoney(total, language)}</span>
      </p>
    </>
  );
}

function ItineraryStep({
  register,
  errors,
  days,
}: StepProps & { days: { id: string; dayNo: number }[] }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const fieldError = useFieldError();

  if (days.length === 0) {
    return <EmptyState title={t('wizard.datesFirst')} />;
  }

  return (
    <ol className="relative flex flex-col gap-5 border-l-2 border-dashed border-hill/20 pl-6 sm:pl-8">
      {days.map((day, index) => (
        <li key={day.id} className="relative rounded-xl p-5 ring-1 ring-hill/10">
          <span
            aria-hidden="true"
            className="absolute -left-[2.6rem] top-5 flex h-9 w-9 items-center justify-center rounded-full bg-turmeric font-display text-sm font-bold text-night ring-4 ring-white sm:-left-[3.1rem]"
          >
            {formatCount(day.dayNo, language)}
          </span>
          <p className="font-display font-semibold text-hill">
            {t('trip.day', { day: formatCount(day.dayNo, language) })}
          </p>
          <div className="mt-4 grid gap-4 sm:grid-cols-[1fr_12rem]">
            <TextField
              id={`day-${index}-title`}
              label={t('wizard.dayTitle')}
              error={fieldError(errors.itinerary?.[index]?.title?.message)}
              {...register(`itinerary.${index}.title`)}
            />
            <SelectField
              id={`day-${index}-difficulty`}
              label={t('wizard.difficulty')}
              {...register(`itinerary.${index}.difficulty`)}
            >
              {difficulties.map((difficulty: Difficulty) => (
                <option key={difficulty} value={difficulty}>
                  {t(`difficulty.${difficulty}`)}
                </option>
              ))}
            </SelectField>
            <div className="sm:col-span-2">
              <TextAreaField
                id={`day-${index}-details`}
                label={t('wizard.dayDetails')}
                error={fieldError(errors.itinerary?.[index]?.details?.message)}
                {...register(`itinerary.${index}.details`)}
              />
            </div>
          </div>
        </li>
      ))}
    </ol>
  );
}

function ReviewStep({
  values,
  total,
  language,
  canPublish,
  status,
}: {
  values: TripForm;
  total: number;
  language: 'bn' | 'en';
  canPublish: boolean;
  status: TripDetail['status'];
}) {
  const { t } = useTranslation();
  const dates = useMemo(
    () =>
      isoDate.test(values.startDate) && isoDate.test(values.endDate)
        ? formatDateRange(values.startDate, values.endDate, language)
        : '',
    [values.startDate, values.endDate, language],
  );

  const facts: { icon: LucideIcon; label: string; value: string }[] = [
    { icon: CalendarDays, label: t('trip.dates'), value: dates },
    { icon: MapPin, label: t('trip.meetingPoint'), value: values.meetingPoint },
    { icon: Users, label: t('trip.seats'), value: formatCount(values.seats, language) },
    { icon: Users, label: t('trip.group'), value: t(`groupType.${values.groupType}`) },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h3 className="text-2xl font-semibold">{values.title}</h3>
        <p className="mt-2 leading-relaxed text-deep/80">{values.summary}</p>
      </div>

      <dl className="grid gap-3 sm:grid-cols-2">
        {facts.map(({ icon: Icon, label, value }) => (
          <div key={label} className="flex items-center gap-3 rounded-xl bg-mist p-4">
            <Icon aria-hidden="true" className="h-5 w-5 shrink-0 text-hill" />
            <div className="min-w-0">
              <dt className="text-xs text-deep/60">{label}</dt>
              <dd className="truncate font-semibold text-deep">{value}</dd>
            </div>
          </div>
        ))}
      </dl>

      {values.itinerary.length > 0 && (
        <div>
          <p className="font-display font-semibold text-deep">{t('trip.itinerary')}</p>
          <ol className="mt-3 flex flex-col gap-2">
            {[...values.itinerary]
              .sort((a, b) => a.dayNo - b.dayNo)
              .map((day) => (
                <li key={day.dayNo} className="flex items-center gap-3 text-sm">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-turmeric text-xs font-bold text-night">
                    {formatCount(day.dayNo, language)}
                  </span>
                  <span className="flex-1 font-medium text-deep">{day.title}</span>
                  <span className="text-deep/60">{t(`difficulty.${day.difficulty}`)}</span>
                </li>
              ))}
          </ol>
        </div>
      )}

      <p className="flex items-baseline justify-between rounded-xl bg-hill px-5 py-4 font-display text-lg font-semibold text-white">
        <span>{t('trip.total')}</span>
        <span className="text-2xl font-bold">{formatMoney(total, language)}</span>
      </p>
      {status === 'Draft' && !canPublish && (
        <p role="note" className="flex gap-3 rounded-xl bg-amber-50 p-4 text-sm text-amber-900">
          <TriangleAlert aria-hidden="true" className="h-5 w-5 shrink-0" />
          <span>
            {t('wizard.publishNeedsVerification')}{' '}
            <Link to="/account/verify" className="font-semibold underline">
              {t('verification.start')}
            </Link>
          </span>
        </p>
      )}
    </div>
  );
}

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  CalendarDays,
  MapPin,
  OctagonX,
  Pencil,
  Plus,
  ShieldCheck,
  SlidersHorizontal,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import {
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
  TextAreaField,
} from '@/components/Field';
import { Photo } from '@/components/ui/Photo';
import { EmptyState, ErrorState } from '@/components/States';
import type { DestinationStatus, DestinationSummary } from '@/features/trips/tripsApi';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage, type Language } from '@/lib/format';
import { adminApi } from './adminApi';
import { AdminPageHeader, CardGridSkeleton, FilterChips, StatusPill } from './AdminUi';
import {
  adminCardClass,
  smallPrimaryButtonClass,
  smallSecondaryButtonClass,
  type Tone,
} from './adminStyles';
import { DestinationEditor } from './DestinationEditor';
import { permissions, usePermissions } from './permissions';

const statuses: readonly DestinationStatus[] = ['Open', 'Caution', 'Closed'];

const statusTone: Record<DestinationStatus, Tone> = {
  Open: 'good',
  Caution: 'warn',
  Closed: 'bad',
};

const statusIcon: Record<DestinationStatus, LucideIcon> = {
  Open: ShieldCheck,
  Caution: TriangleAlert,
  Closed: OctagonX,
};

/** The note under a destination's photo, tinted by its status. */
const noteClass: Record<DestinationStatus, string> = {
  Open: 'bg-emerald-50/70 text-emerald-900 ring-emerald-600/10',
  Caution: 'bg-turmeric/10 text-deep ring-turmeric/25',
  Closed: 'bg-jamdani/5 text-deep ring-jamdani/15',
};

/** The choice in the status dialog, when it is the one picked. */
const choiceClass: Record<DestinationStatus, string> = {
  Open: 'bg-emerald-50 text-emerald-900 ring-2 ring-emerald-600/60',
  Caution: 'bg-turmeric/15 text-deep ring-2 ring-turmeric',
  Closed: 'bg-jamdani/10 text-jamdani ring-2 ring-jamdani/70',
};

// Closed places first, then those on caution: what the desk is watching sits at the top.
const urgency: Record<DestinationStatus, number> = { Closed: 0, Caution: 1, Open: 2 };

type StatusFilter = 'all' | DestinationStatus;

/**
 * The safety desk sets each destination Open, Caution or Closed, with a note in both languages.
 * Closing one cancels every trip there and refunds everyone in full, so it asks twice.
 *
 * Each destination is a card with its photo, so the desk recognises a place at a glance, and
 * the filter chips count how many are in each state.
 */
export function DestinationAlertsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const destinations = useDestinations();
  const [editing, setEditing] = useState<DestinationSummary | null>(null);
  const [details, setDetails] = useState<DestinationSummary | 'new' | null>(null);
  const [filter, setFilter] = useState<StatusFilter>('all');
  // Editing a place's details is its own permission, separate from setting its safety status.
  const { can } = usePermissions();
  const canEditDetails = can(permissions.destinationsManage);

  const all = destinations.data ?? [];
  const countOf = (status: DestinationStatus) =>
    all.filter((destination) => destination.status === status).length;
  const shown = all
    .filter((destination) => filter === 'all' || destination.status === filter)
    .sort((a, b) => urgency[a.status] - urgency[b.status]);

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.safety')}
        title={t('admin.destinations.title')}
        description={t('admin.destinations.lead')}
        actions={
          canEditDetails && (
            <button type="button" className={primaryButtonClass} onClick={() => setDetails('new')}>
              <Plus aria-hidden="true" className="h-4 w-4" />
              {t('admin.destinations.add')}
            </button>
          )
        }
      />

      {all.length > 0 && (
        <FilterChips
          label={t('admin.destinations.filterLabel')}
          value={filter}
          onChange={setFilter}
          options={[
            { value: 'all', label: t('admin.ui.all'), count: all.length },
            ...statuses.map((status) => ({
              value: status,
              label: t(`destination.status.${status}`),
              count: countOf(status),
              tone: statusTone[status],
            })),
          ]}
        />
      )}

      {destinations.isPending && <CardGridSkeleton className="h-96" />}

      {destinations.isError && (
        <ErrorState
          message={errorText(destinations.error, t)}
          onRetry={() => void destinations.refetch()}
        />
      )}

      {destinations.data?.length === 0 && <EmptyState title={t('common.empty')} />}

      {all.length > 0 && shown.length === 0 && (
        <EmptyState title={t('admin.destinations.noneWithStatus')} />
      )}

      {shown.length > 0 && (
        <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {shown.map((destination) => (
            <li key={destination.slug} className="flex">
              <DestinationCard
                destination={destination}
                language={language}
                onChangeStatus={() => setEditing(destination)}
                onEditDetails={canEditDetails ? () => setDetails(destination) : undefined}
              />
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <StatusDialog
          key={editing.slug}
          destination={editing}
          language={language}
          onClose={() => setEditing(null)}
        />
      )}

      {details && (
        <DestinationEditor
          key={details === 'new' ? 'new' : details.slug}
          destination={details === 'new' ? null : details}
          onClose={() => setDetails(null)}
        />
      )}
    </div>
  );
}

/** One destination: its photo with the status over it, the note, and the desk's actions. */
function DestinationCard({
  destination,
  language,
  onChangeStatus,
  onEditDetails,
}: {
  destination: DestinationSummary;
  language: Language;
  onChangeStatus: () => void;
  onEditDetails: (() => void) | undefined;
}) {
  const { t } = useTranslation();
  const name = language === 'bn' ? destination.nameBn : destination.name;
  const division = language === 'bn' ? destination.divisionBn : destination.division;
  const note = language === 'bn' ? destination.statusNoteBn : destination.statusNote;
  const trips = asNumber(destination.upcomingTrips);
  const NoteIcon = statusIcon[destination.status];

  return (
    <article className={adminCardClass}>
      <div className="relative h-44 shrink-0">
        <Photo
          slug={destination.slug}
          kind={destination.kind}
          cut="card"
          sizes="(min-width: 1280px) 22rem, (min-width: 640px) 45vw, 100vw"
          decorative
          className="absolute! inset-0"
          imgClassName="transition-transform duration-700 group-hover/card:scale-105 motion-reduce:transition-none"
        />
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-linear-to-t from-night/85 via-night/25 to-night/5"
        />
        <StatusPill
          tone={statusTone[destination.status]}
          icon={statusIcon[destination.status]}
          onPhoto
          className="absolute left-4 top-4"
        >
          {t(`destination.status.${destination.status}`)}
        </StatusPill>
        <span className="absolute right-4 top-4 rounded-full bg-night/45 px-2.5 py-1 text-xs font-semibold text-white ring-1 ring-white/20 backdrop-blur-sm">
          {t(`kind.${destination.kind}`)}
        </span>
        <div className="absolute inset-x-4 bottom-3.5 text-white">
          <h3 className="font-display text-xl font-semibold leading-tight text-white! drop-shadow-sm">
            {name}
          </h3>
          <p className="mt-0.5 flex items-center gap-1 text-sm text-white/80">
            <MapPin aria-hidden="true" className="h-3.5 w-3.5" />
            {division}
          </p>
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-4 p-5">
        <p
          className={`flex items-start gap-2.5 rounded-2xl px-3.5 py-3 text-sm leading-relaxed ring-1 ${noteClass[destination.status]}`}
        >
          <NoteIcon
            aria-hidden="true"
            className={`mt-0.5 h-4 w-4 shrink-0 ${
              destination.status === 'Open'
                ? 'text-emerald-600'
                : destination.status === 'Caution'
                  ? 'text-ochre'
                  : 'text-jamdani'
            }`}
          />
          <span>{note ?? t('admin.destinations.noNote')}</span>
        </p>

        <p className="flex items-center gap-2 text-sm text-deep/65">
          <CalendarDays aria-hidden="true" className="h-4 w-4 text-hill/70" />
          {trips > 0
            ? t('admin.destinations.upcomingTrips', {
                count: trips,
                value: formatCount(trips, language),
              })
            : t('admin.destinations.noUpcomingTrips')}
        </p>

        <div className="mt-auto flex flex-wrap gap-2 border-t border-hill/8 pt-4">
          <button type="button" className={smallPrimaryButtonClass} onClick={onChangeStatus}>
            <SlidersHorizontal aria-hidden="true" className="h-4 w-4" />
            {t('admin.destinations.change')}
          </button>
          {onEditDetails && (
            <button type="button" className={smallSecondaryButtonClass} onClick={onEditDetails}>
              <Pencil aria-hidden="true" className="h-4 w-4" />
              {t('admin.destinations.editDetails')}
            </button>
          )}
        </div>
      </div>
    </article>
  );
}

function StatusDialog({
  destination,
  language,
  onClose,
}: {
  destination: DestinationSummary;
  language: Language;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<DestinationStatus>(destination.status);
  const [note, setNote] = useState(destination.statusNote ?? '');
  const [noteBn, setNoteBn] = useState(destination.statusNoteBn ?? '');
  const [confirmed, setConfirmed] = useState(false);

  const save = useMutation({
    mutationFn: () =>
      adminApi.setDestinationStatus(destination.slug, {
        status,
        note: note.trim() || null,
        noteBn: noteBn.trim() || null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['destinations'] });
      void queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
      onClose();
    },
  });

  const needsNotes = status !== 'Open';
  const notesMissing = needsNotes && (note.trim() === '' || noteBn.trim() === '');
  const closing = status === 'Closed' && destination.status !== 'Closed';

  return (
    <Dialog
      open
      title={t('admin.destinations.dialogTitle', {
        name: language === 'bn' ? destination.nameBn : destination.name,
      })}
      onClose={onClose}
    >
      <form
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          save.mutate();
        }}
      >
        <fieldset>
          <legend className="mb-2 text-sm font-semibold text-deep">
            {t('admin.destinations.status')}
          </legend>
          <div className="grid grid-cols-3 gap-2">
            {statuses.map((option) => {
              const Icon = statusIcon[option];
              const picked = status === option;
              return (
                <label
                  key={option}
                  className={`flex cursor-pointer flex-col items-center gap-1.5 rounded-2xl px-2 py-3 text-sm font-semibold transition has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-turmeric ${
                    picked ? choiceClass[option] : 'text-deep/70 ring-1 ring-hill/15 hover:bg-mist'
                  }`}
                >
                  <input
                    type="radio"
                    name="status"
                    value={option}
                    checked={picked}
                    onChange={() => {
                      setStatus(option);
                      setConfirmed(false);
                    }}
                    className="sr-only"
                  />
                  <Icon aria-hidden="true" className="h-5 w-5" />
                  {t(`destination.status.${option}`)}
                </label>
              );
            })}
          </div>
        </fieldset>

        <TextAreaField
          id="note-en"
          label={t('admin.destinations.noteEn')}
          value={note}
          maxLength={300}
          rows={2}
          onChange={(event) => setNote(event.target.value)}
        />
        <TextAreaField
          id="note-bn"
          label={t('admin.destinations.noteBn')}
          value={noteBn}
          maxLength={300}
          rows={2}
          lang="bn"
          onChange={(event) => setNoteBn(event.target.value)}
        />
        {notesMissing && (
          <p className="text-sm text-deep/70">{t('admin.destinations.notesHint')}</p>
        )}

        {closing && (
          <label className="flex items-start gap-2.5 rounded-2xl bg-jamdani/8 p-3.5 text-sm text-deep ring-1 ring-jamdani/20">
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(event) => setConfirmed(event.target.checked)}
              className="mt-1 accent-jamdani"
            />
            <span>{t('admin.destinations.closeWarning')}</span>
          </label>
        )}

        {save.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(save.error, t)}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            className={closing ? dangerButtonClass : primaryButtonClass}
            disabled={save.isPending || notesMissing || (closing && !confirmed)}
          >
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}

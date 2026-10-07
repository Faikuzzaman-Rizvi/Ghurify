import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { Dialog } from '@/components/Dialog';
import {
  cardClass,
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
  TextAreaField,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import type { DestinationStatus, DestinationSummary } from '@/features/trips/tripsApi';
import { useMyProfile } from '@/features/auth/useProfile';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { adminApi } from './adminApi';
import { DestinationEditor } from './DestinationEditor';

const statuses: readonly DestinationStatus[] = ['Open', 'Caution', 'Closed'];

const badge: Record<DestinationStatus, string> = {
  Open: 'bg-emerald-50 text-emerald-800',
  Caution: 'bg-turmeric/20 text-deep',
  Closed: 'bg-jamdani/10 text-jamdani',
};

/**
 * The safety desk sets each destination Open, Caution or Closed, with a note in both languages.
 * Closing one cancels every trip there and refunds everyone in full, so it asks twice.
 */
export function DestinationAlertsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const destinations = useDestinations();
  const [editing, setEditing] = useState<DestinationSummary | null>(null);
  const [details, setDetails] = useState<DestinationSummary | 'new' | null>(null);
  const { data: profile } = useMyProfile();
  const isAdmin = profile?.roles.includes('Admin') ?? false;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
          {t('admin.destinations.title')}
        </h2>
        {isAdmin && (
          <button type="button" className={secondaryButtonClass} onClick={() => setDetails('new')}>
            {t('admin.destinations.add')}
          </button>
        )}
      </header>

      {destinations.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {destinations.isError && (
        <ErrorState
          message={errorText(destinations.error, t)}
          onRetry={() => void destinations.refetch()}
        />
      )}

      {destinations.data?.length === 0 && <EmptyState title={t('common.empty')} />}

      {destinations.data && destinations.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {destinations.data.map((destination) => {
            const note = language === 'bn' ? destination.statusNoteBn : destination.statusNote;
            return (
              <li key={destination.slug}>
                <article
                  className={`${cardClass} flex flex-wrap items-center justify-between gap-3`}
                >
                  <div>
                    <p className="font-bold text-deep">
                      {language === 'bn' ? destination.nameBn : destination.name}{' '}
                      <span
                        className={`ml-1 rounded-full px-2 py-0.5 text-xs font-semibold ${badge[destination.status]}`}
                      >
                        {t(`destination.status.${destination.status}`)}
                      </span>
                    </p>
                    {note && <p className="mt-1 text-sm text-deep/70">{note}</p>}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {isAdmin && (
                      <button
                        type="button"
                        className={secondaryButtonClass}
                        onClick={() => setDetails(destination)}
                      >
                        {t('admin.destinations.editDetails')}
                      </button>
                    )}
                    <button
                      type="button"
                      className={secondaryButtonClass}
                      onClick={() => setEditing(destination)}
                    >
                      {t('admin.destinations.change')}
                    </button>
                  </div>
                </article>
              </li>
            );
          })}
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

function StatusDialog({
  destination,
  language,
  onClose,
}: {
  destination: DestinationSummary;
  language: 'bn' | 'en';
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
        className="flex flex-col gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          save.mutate();
        }}
      >
        <fieldset className="flex flex-wrap gap-2">
          <legend className="mb-1 text-sm font-medium text-deep">
            {t('admin.destinations.status')}
          </legend>
          {statuses.map((option) => (
            <label
              key={option}
              className={`cursor-pointer rounded-full px-3 py-1.5 text-sm font-medium ring-1 ${
                status === option ? 'bg-hill text-white ring-hill' : 'text-deep ring-hill/20'
              }`}
            >
              <input
                type="radio"
                name="status"
                value={option}
                checked={status === option}
                onChange={() => {
                  setStatus(option);
                  setConfirmed(false);
                }}
                className="sr-only"
              />
              {t(`destination.status.${option}`)}
            </label>
          ))}
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
          <label className="flex items-start gap-2 rounded-2xl bg-jamdani/10 p-3 text-sm text-deep">
            <input
              type="checkbox"
              checked={confirmed}
              onChange={(event) => setConfirmed(event.target.checked)}
              className="mt-1"
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

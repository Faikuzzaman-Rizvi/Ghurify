import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { CircleCheck, Plus } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Dialog } from '@/components/Dialog';
import {
  cardClass,
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextField,
} from '@/components/Field';
import { EmptyState, ErrorState } from '@/components/States';
import { useDestinations } from '@/features/trips/useTrips';
import { mapLink } from '@/features/safety/safetyApi';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { adminApi, type EmergencyPointView } from './adminApi';

const kinds = [1, 2, 3] as const;

/**
 * The police stations and hospitals offered with every SOS. Seeded positions are approximate;
 * the desk corrects them, adds phone numbers, and marks each one checked.
 */
export function EmergencyPointsPage() {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [editing, setEditing] = useState<EmergencyPointView | 'new' | null>(null);

  const points = useQuery({
    queryKey: ['admin', 'emergency-points'],
    queryFn: ({ signal }) => adminApi.emergencyPoints(signal),
  });

  const unchecked = points.data?.filter((point) => !point.checkedOn).length ?? 0;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="font-display text-2xl font-semibold text-deep sm:text-[1.75rem]">
            {t('admin.points.title')}
          </h2>
          {points.data && (
            <p className="text-sm text-deep/70">
              {t('admin.points.unchecked', { count: unchecked })}
            </p>
          )}
        </div>
        <button type="button" className={primaryButtonClass} onClick={() => setEditing('new')}>
          <Plus aria-hidden="true" className="h-4 w-4" />
          {t('admin.points.add')}
        </button>
      </header>

      {points.isPending && (
        <div role="status" className="h-40 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}
      {points.isError && (
        <ErrorState message={errorText(points.error, t)} onRetry={() => void points.refetch()} />
      )}
      {points.data?.length === 0 && <EmptyState title={t('admin.points.empty')} />}

      {points.data && points.data.length > 0 && (
        <ul className="flex flex-col gap-2">
          {points.data.map((point) => (
            <li
              key={String(point.id)}
              className={`${cardClass} flex flex-wrap items-center justify-between gap-3 p-4!`}
            >
              <div className="min-w-0">
                <p className="font-semibold text-deep">
                  {language === 'bn' ? point.nameBn : point.name}{' '}
                  <span className="text-sm font-normal text-deep/60">
                    · {t(`admin.points.kinds.${point.kind}`)}
                    {point.destinationName ? ` · ${point.destinationName}` : ''}
                  </span>
                </p>
                <p className="text-sm text-deep/70">
                  {point.phone ? (
                    <a href={`tel:${point.phone}`} className="text-hill underline">
                      {point.phone}
                    </a>
                  ) : (
                    t('admin.points.noPhone')
                  )}{' '}
                  ·{' '}
                  <a
                    href={mapLink(point.latitude, point.longitude)}
                    target="_blank"
                    rel="noreferrer"
                    className="text-hill underline"
                  >
                    {t('admin.sos.map')}
                  </a>
                </p>
              </div>
              <div className="flex items-center gap-3">
                {point.checkedOn ? (
                  <span className="inline-flex items-center gap-1 text-xs font-semibold text-emerald-700">
                    <CircleCheck aria-hidden="true" className="h-4 w-4" />
                    {t('admin.points.checked', { name: point.checkedBy ?? '' })}
                  </span>
                ) : (
                  <span className="rounded-full bg-turmeric/20 px-2 py-0.5 text-xs font-semibold text-deep">
                    {t('admin.points.notChecked')}
                  </span>
                )}
                <button
                  type="button"
                  className={secondaryButtonClass}
                  onClick={() => setEditing(point)}
                >
                  {t('admin.points.edit')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <PointDialog
          key={editing === 'new' ? 'new' : String(editing.id)}
          point={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

function PointDialog({
  point,
  onClose,
}: {
  point: EmergencyPointView | null;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const destinations = useDestinations();
  const [form, setForm] = useState({
    destinationSlug: point?.destinationSlug ?? '',
    kind: point ? asNumber(point.kind) : 1,
    name: point?.name ?? '',
    nameBn: point?.nameBn ?? '',
    phone: point?.phone ?? '',
    latitude: point ? String(point.latitude) : '',
    longitude: point ? String(point.longitude) : '',
    checked: point ? point.checkedOn !== null : false,
  });

  const save = useMutation({
    mutationFn: () =>
      adminApi.saveEmergencyPoint({
        id: point ? asNumber(point.id) : null,
        destinationSlug: form.destinationSlug || null,
        kind: form.kind,
        name: form.name,
        nameBn: form.nameBn,
        phone: form.phone.trim() || null,
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        checked: form.checked,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'emergency-points'] });
      onClose();
    },
  });

  const set = (key: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  return (
    <Dialog
      open
      title={point ? t('admin.points.editTitle') : t('admin.points.add')}
      onClose={onClose}
    >
      <form
        className="grid gap-3 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          save.mutate();
        }}
      >
        <SelectField
          id="point-kind"
          label={t('admin.points.kind')}
          value={form.kind}
          onChange={(event) =>
            setForm((current) => ({ ...current, kind: Number(event.target.value) }))
          }
        >
          {kinds.map((kind) => (
            <option key={kind} value={kind}>
              {t(`admin.points.kinds.${kind}`)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="point-destination"
          label={t('admin.points.destination')}
          value={form.destinationSlug}
          onChange={set('destinationSlug')}
        >
          <option value="">{t('admin.points.noDestination')}</option>
          {(destinations.data ?? []).map((destination) => (
            <option key={destination.slug} value={destination.slug}>
              {language === 'bn' ? destination.nameBn : destination.name}
            </option>
          ))}
        </SelectField>
        <TextField
          id="point-name"
          label={t('admin.points.name')}
          value={form.name}
          maxLength={150}
          onChange={set('name')}
        />
        <TextField
          id="point-name-bn"
          label={t('admin.points.nameBn')}
          value={form.nameBn}
          maxLength={150}
          lang="bn"
          onChange={set('nameBn')}
        />
        <TextField
          id="point-phone"
          label={t('admin.points.phone')}
          value={form.phone}
          inputMode="tel"
          maxLength={20}
          onChange={set('phone')}
        />
        <div className="grid grid-cols-2 gap-2">
          <TextField
            id="point-lat"
            label={t('admin.points.latitude')}
            value={form.latitude}
            inputMode="decimal"
            onChange={set('latitude')}
          />
          <TextField
            id="point-long"
            label={t('admin.points.longitude')}
            value={form.longitude}
            inputMode="decimal"
            onChange={set('longitude')}
          />
        </div>
        <label className="flex items-start gap-2 text-sm text-deep sm:col-span-2">
          <input
            type="checkbox"
            checked={form.checked}
            onChange={(event) =>
              setForm((current) => ({ ...current, checked: event.target.checked }))
            }
            className="mt-1"
          />
          {t('admin.points.confirmChecked')}
        </label>
        {save.isError && (
          <p role="alert" className="text-sm text-jamdani sm:col-span-2">
            {errorText(save.error, t)}
          </p>
        )}
        <div className="flex justify-end gap-2 sm:col-span-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button type="submit" className={primaryButtonClass} disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}

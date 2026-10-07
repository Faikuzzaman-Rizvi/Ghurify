import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { Dialog } from '@/components/Dialog';
import {
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextAreaField,
  TextField,
} from '@/components/Field';
import type { DestinationKind, DestinationSummary } from '@/features/trips/tripsApi';
import { errorText } from '@/lib/errors';
import { adminApi } from './adminApi';

const kinds: readonly DestinationKind[] = [
  'Hills',
  'Beach',
  'Island',
  'Forest',
  'Wetland',
  'TeaGarden',
  'Lake',
  'River',
];

/**
 * Adds a destination, or edits one's name, division and summary in both languages, and its map
 * position. The slug (its web address) is chosen once and never changes.
 */
export function DestinationEditor({
  destination,
  onClose,
}: {
  destination: DestinationSummary | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const initialKind: DestinationKind = destination?.kind ?? 'Hills';
  const [form, setForm] = useState({
    slug: destination?.slug ?? '',
    name: destination?.name ?? '',
    nameBn: destination?.nameBn ?? '',
    division: destination?.division ?? '',
    divisionBn: destination?.divisionBn ?? '',
    summary: destination?.summary ?? '',
    summaryBn: destination?.summaryBn ?? '',
    kind: initialKind,
    latitude: destination?.latitude != null ? String(destination.latitude) : '',
    longitude: destination?.longitude != null ? String(destination.longitude) : '',
  });

  const save = useMutation({
    mutationFn: () =>
      adminApi.saveDestination(form.slug.trim(), {
        name: form.name,
        nameBn: form.nameBn,
        division: form.division,
        divisionBn: form.divisionBn,
        summary: form.summary,
        summaryBn: form.summaryBn,
        kind: form.kind,
        latitude: form.latitude ? Number(form.latitude) : null,
        longitude: form.longitude ? Number(form.longitude) : null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['destinations'] });
      onClose();
    },
  });

  const set = (key: Exclude<keyof typeof form, 'kind'>) => (event: { target: { value: string } }) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  return (
    <Dialog
      open
      title={destination ? t('admin.destinations.editTitle') : t('admin.destinations.add')}
      onClose={onClose}
    >
      <form
        className="grid gap-3 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          save.mutate();
        }}
      >
        <TextField
          id="destination-slug"
          label={t('admin.destinations.slug')}
          hint={t('admin.destinations.slugHint')}
          value={form.slug}
          maxLength={60}
          readOnly={destination !== null}
          onChange={set('slug')}
        />
        <SelectField
          id="destination-kind"
          label={t('admin.destinations.kind')}
          value={form.kind}
          onChange={(event) =>
            setForm((current) => ({ ...current, kind: event.target.value as DestinationKind }))
          }
        >
          {kinds.map((kind) => (
            <option key={kind} value={kind}>
              {t(`kind.${kind}`)}
            </option>
          ))}
        </SelectField>
        <TextField
          id="destination-name"
          label={t('admin.destinations.nameEn')}
          value={form.name}
          maxLength={100}
          onChange={set('name')}
        />
        <TextField
          id="destination-name-bn"
          label={t('admin.destinations.nameBn')}
          value={form.nameBn}
          maxLength={100}
          lang="bn"
          onChange={set('nameBn')}
        />
        <TextField
          id="destination-division"
          label={t('admin.destinations.divisionEn')}
          value={form.division}
          maxLength={50}
          onChange={set('division')}
        />
        <TextField
          id="destination-division-bn"
          label={t('admin.destinations.divisionBn')}
          value={form.divisionBn}
          maxLength={50}
          lang="bn"
          onChange={set('divisionBn')}
        />
        <TextAreaField
          id="destination-summary"
          label={t('admin.destinations.summaryEn')}
          value={form.summary}
          maxLength={400}
          rows={2}
          onChange={set('summary')}
        />
        <TextAreaField
          id="destination-summary-bn"
          label={t('admin.destinations.summaryBn')}
          value={form.summaryBn}
          maxLength={400}
          rows={2}
          lang="bn"
          onChange={set('summaryBn')}
        />
        <TextField
          id="destination-lat"
          label={t('admin.points.latitude')}
          value={form.latitude}
          inputMode="decimal"
          onChange={set('latitude')}
        />
        <TextField
          id="destination-long"
          label={t('admin.points.longitude')}
          value={form.longitude}
          inputMode="decimal"
          onChange={set('longitude')}
        />
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

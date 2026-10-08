import { useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { MapPin, Mountain } from 'lucide-react';

import { Dialog } from '@/components/Dialog';
import {
  DateField,
  primaryButtonClass,
  secondaryButtonClass,
  SelectField,
  TextAreaField,
  TextField,
} from '@/components/Field';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { toLanguage, todayInDhaka } from '@/lib/format';
import { toLatLng } from '@/components/ui/mapProjection';
import { districtAt, districtBySlug } from './districts';
import { PhotoPicker } from './PhotoPicker';
import PinPicker from './PinPicker';
import { divisions, maxPhotosPerVisit, type Division } from './travelApi';
import { usePhotoUploads } from './usePhotoUploads';
import { useAddVisit } from './useTravelMap';

/** Mirrors AddVisitHandler: a destination, or a named pin with its division; a past date. */
const schema = z
  .object({
    mode: z.enum(['destination', 'pin']),
    destinationSlug: z.string(),
    placeName: z.string().trim(),
    division: z.string(),
    latitude: z.number().nullable(),
    longitude: z.number().nullable(),
    visitedOn: z.string().min(1, { message: 'date' }),
    note: z.string().max(500, { message: 'note' }),
  })
  .superRefine((value, context) => {
    if (value.mode === 'destination' && !value.destinationSlug) {
      context.addIssue({ code: 'custom', path: ['destinationSlug'], message: 'destination' });
    }
    if (value.mode === 'pin') {
      if (value.placeName.length < 2 || value.placeName.length > 120) {
        context.addIssue({ code: 'custom', path: ['placeName'], message: 'name' });
      }
      if (!value.division) {
        context.addIssue({ code: 'custom', path: ['division'], message: 'division' });
      }
      if (value.latitude === null || value.longitude === null) {
        context.addIssue({ code: 'custom', path: ['latitude'], message: 'pin' });
      } else if (!districtAt(value.latitude, value.longitude)) {
        context.addIssue({ code: 'custom', path: ['latitude'], message: 'outside' });
      }
    }
    if (value.visitedOn && value.visitedOn > todayInDhaka()) {
      context.addIssue({ code: 'custom', path: ['visitedOn'], message: 'future' });
    }
  });

type PlaceForm = z.infer<typeof schema>;

/**
 * Adds a place someone has been to their map: one of Ghurify's destinations, or anywhere else in
 * Bangladesh by name and a pin on the map (the pin tells its district and division). Ghurify trips
 * add themselves when they end. Opened for a district, it starts with a pin in the middle of it.
 */
export function AddPlaceDialog({
  open,
  onClose,
  onAdded,
  district = null,
}: {
  open: boolean;
  onClose: () => void;
  /** The new visit's id, so the page can show its pin. */
  onAdded: (visitId: number) => void;
  /** A district slug to start in: its name, its division and a pin at its centre. */
  district?: string | null;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const { data: destinations } = useDestinations();
  const add = useAddVisit();
  const photos = usePhotoUploads(maxPhotosPerVisit);
  const [submitted, setSubmitted] = useState(false);
  const start = district ? districtBySlug.get(district) : undefined;
  const startPoint = start ? toLatLng(start.label[0], start.label[1]) : null;
  const [moved, setMoved] = useState(false);

  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<PlaceForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      mode: start ? 'pin' : 'destination',
      destinationSlug: '',
      placeName: start ? t(`district.${start.slug}`) : '',
      division: start?.division ?? '',
      latitude: startPoint ? Math.round(startPoint[0] * 1e6) / 1e6 : null,
      longitude: startPoint ? Math.round(startPoint[1] * 1e6) / 1e6 : null,
      visitedOn: '',
      note: '',
    },
  });

  const mode = useWatch({ control, name: 'mode' });
  const latitude = useWatch({ control, name: 'latitude' });
  const longitude = useWatch({ control, name: 'longitude' });
  const point: [number, number] | null =
    latitude !== null && longitude !== null ? [latitude, longitude] : null;
  const pinDistrict = point ? districtAt(point[0], point[1]) : null;
  const message = (key: string | undefined) => (key ? t(`travel.add.errors.${key}`) : undefined);

  const submit = handleSubmit((form) => {
    setSubmitted(true);
    // The photos are already uploaded: they go with the place by id, all or nothing.
    const withPhotos = photos.mediaIds.length > 0 ? { mediaIds: photos.mediaIds } : {};
    add.mutate(
      form.mode === 'destination'
        ? {
            destinationSlug: form.destinationSlug,
            placeName: null,
            division: null,
            latitude: null,
            longitude: null,
            visitedOn: form.visitedOn,
            note: form.note.trim() || null,
            ...withPhotos,
          }
        : {
            destinationSlug: null,
            placeName: form.placeName,
            division: form.division as Division,
            latitude: form.latitude,
            longitude: form.longitude,
            visitedOn: form.visitedOn,
            note: form.note.trim() || null,
            ...withPhotos,
          },
      { onSuccess: (created) => onAdded(Number(created.id)) },
    );
  });

  const choice = (value: PlaceForm['mode'], label: string, Icon: typeof MapPin) => (
    <label
      className={`flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-full px-3 py-2 text-sm font-semibold transition has-focus-visible:ring-2 has-focus-visible:ring-turmeric ${
        mode === value ? 'bg-hill text-white shadow' : 'text-deep/70 hover:text-deep'
      }`}
    >
      <input type="radio" value={value} className="sr-only" {...register('mode')} />
      <Icon aria-hidden="true" className="h-4 w-4" />
      {label}
    </label>
  );

  return (
    <Dialog open={open} title={t('travel.add.title')} onClose={onClose}>
      <form className="flex flex-col gap-4" noValidate onSubmit={(event) => void submit(event)}>
        <fieldset>
          <legend className="sr-only">{t('travel.add.which')}</legend>
          <div className="flex gap-1 rounded-full bg-mist p-1">
            {choice('destination', t('travel.add.destination'), Mountain)}
            {choice('pin', t('travel.add.elsewhere'), MapPin)}
          </div>
        </fieldset>

        {mode === 'destination' ? (
          <SelectField
            id="visit-destination"
            label={t('travel.add.destinationLabel')}
            error={message(errors.destinationSlug?.message)}
            {...register('destinationSlug')}
          >
            <option value="">{t('travel.add.choose')}</option>
            {(destinations ?? []).map((item) => (
              <option key={item.slug} value={item.slug}>
                {language === 'bn' ? item.nameBn : item.name}
              </option>
            ))}
          </SelectField>
        ) : (
          <>
            <TextField
              id="visit-place"
              label={t('travel.add.placeName')}
              hint={t('travel.add.placeHint')}
              maxLength={120}
              error={message(errors.placeName?.message)}
              {...register('placeName')}
            />
            <SelectField
              id="visit-division"
              label={t('travel.add.division')}
              error={message(errors.division?.message)}
              {...register('division')}
            >
              <option value="">{t('travel.add.choose')}</option>
              {divisions.map((division) => (
                <option key={division} value={division}>
                  {t(`division.${division}`)}
                </option>
              ))}
            </SelectField>
            <div className="flex flex-col gap-1.5">
              <p className="text-sm font-semibold text-deep">{t('travel.add.pin')}</p>
              <PinPicker
                point={point}
                label={t('travel.add.pinLabel')}
                onPick={([lat, lng]) => {
                  setMoved(true);
                  // The pin knows its division: fill it in, so the two always agree.
                  const found = districtAt(lat, lng);
                  if (found) setValue('division', found.division, { shouldValidate: submitted });
                  setValue('latitude', Math.round(lat * 1e6) / 1e6, { shouldValidate: submitted });
                  setValue('longitude', Math.round(lng * 1e6) / 1e6, {
                    shouldValidate: submitted,
                  });
                }}
              />
              <p
                className={`text-xs ${errors.latitude ? 'text-jamdani' : 'text-deep/60'}`}
                role={errors.latitude ? 'alert' : undefined}
              >
                {errors.latitude
                  ? message(errors.latitude.message)
                  : start && !moved
                    ? t('travel.add.pinCentre', { district: t(`district.${start.slug}`) })
                    : pinDistrict
                      ? `${t('travel.add.pinSet')} ${t('travel.add.inDistrict', {
                          district: t(`district.${pinDistrict.slug}`),
                          division: t(`division.${pinDistrict.division}`),
                        })}`
                      : point
                        ? t('travel.add.pinSet')
                        : t('travel.add.pinHint')}
              </p>
            </div>
          </>
        )}

        <Controller
          control={control}
          name="visitedOn"
          render={({ field }) => (
            <DateField
              id="visit-date"
              label={t('travel.add.date')}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              max={todayInDhaka()}
              min="1950-01-01"
              error={message(errors.visitedOn?.message)}
            />
          )}
        />

        <TextAreaField
          id="visit-note"
          label={t('travel.add.note')}
          hint={t('travel.add.noteHint')}
          rows={2}
          maxLength={500}
          error={message(errors.note?.message)}
          {...register('note')}
        />

        <fieldset>
          <legend className="mb-1.5 text-sm font-semibold text-deep">
            {t('travel.photos.optional')}
          </legend>
          <PhotoPicker
            uploads={photos.uploads}
            limit={maxPhotosPerVisit}
            error={photos.error}
            onAdd={photos.add}
            onRemove={photos.remove}
          />
        </fieldset>

        {add.isError && (
          <p role="alert" className="text-sm text-jamdani">
            {errorText(add.error, t)}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            className={primaryButtonClass}
            disabled={add.isPending || photos.uploading}
          >
            {photos.uploading
              ? t('travel.photos.uploading')
              : add.isPending
                ? t('common.saving')
                : t('travel.add.save')}
          </button>
        </div>
      </form>
    </Dialog>
  );
}

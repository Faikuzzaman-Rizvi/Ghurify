import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import {
  ArrowLeft,
  CalendarDays,
  Camera,
  Compass,
  ImagePlus,
  LoaderCircle,
  MapPin,
  PenLine,
  Pencil,
  Tent,
  Trash2,
  UserRound,
  X,
} from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  cardClass,
  DateField,
  dangerButtonClass,
  primaryButtonClass,
  secondaryButtonClass,
  TextAreaField,
} from '@/components/Field';
import { Photo } from '@/components/ui/Photo';
import { errorText } from '@/lib/errors';
import {
  formatCount,
  formatDateRange,
  formatFullDate,
  toLanguage,
  todayInDhaka,
  tripDays,
} from '@/lib/format';
import { PhotoPicker } from './PhotoPicker';
import { PhotoViewer, type ViewedPhoto } from './PhotoViewer';
import {
  maxPhotosPerVisit,
  type PlacePhoto,
  type PlaceVisit,
  type VisitedPlace,
} from './travelApi';
import { usePhotoUploads } from './usePhotoUploads';
import {
  useAddVisitPhotos,
  useEditVisit,
  useRemoveVisit,
  useRemoveVisitPhoto,
} from './useTravelMap';

/**
 * One pin, opened: the place, every visit there (the trip it was, who hosted, how long, the
 * note, the photos they put on it), and photos from their own stories about it. The owner can
 * add photos to a visit, change its note and date, or take it off the map. Any photo opens large.
 */
export function PlaceDetails({
  place,
  editable,
  onBack,
}: {
  place: VisitedPlace;
  /** The owner's own map: notes, photos, editing, and the link to share a story. */
  editable: boolean;
  onBack: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const name = language === 'bn' && place.nameBn ? place.nameBn : place.name;
  const visits = place.visits.length;
  const [viewing, setViewing] = useState<number | null>(null);

  // Their own photos lead: the first is the place's picture, before any stock photo.
  const images = place.photos.filter((photo) => photo.kind === 'Image');
  const cover = images[0];
  const stories = place.photos.filter((photo) => photo.postId !== null);
  const visitOf = (photo: PlacePhoto) =>
    photo.visitId === null
      ? undefined
      : place.visits.find((visit) => asNumber(visit.id) === asNumber(photo.visitId!));
  const viewable: ViewedPhoto[] = images.map((photo) => {
    const visit = visitOf(photo);
    return {
      key: String(photo.mediaId),
      url: photo.url,
      alt: t('travel.details.photoAlt', { place: name }),
      place: name,
      when: visit ? formatFullDate(visit.visitedOn, language) : t('travel.photos.fromStory'),
    };
  });
  const open = (photo: PlacePhoto) => {
    const index = viewable.findIndex((item) => item.key === String(photo.mediaId));
    if (index >= 0) setViewing(index);
  };

  return (
    <article className={`${cardClass} flex flex-col gap-5 overflow-hidden p-0!`} aria-label={name}>
      <header className="relative isolate h-48 overflow-hidden">
        {cover ? (
          <img
            src={cover.url}
            alt=""
            className="absolute inset-0 -z-10 h-full w-full object-cover motion-safe:animate-fade-in"
          />
        ) : place.destinationSlug ? (
          <Photo
            slug={place.destinationSlug}
            kind={place.kind ?? 'Hills'}
            cut="card"
            decorative
            className="absolute! inset-0 -z-10"
          />
        ) : (
          <div
            aria-hidden="true"
            className="absolute inset-0 -z-10 bg-linear-to-br from-turmeric via-ochre to-hill"
          />
        )}
        <div
          aria-hidden="true"
          className="absolute inset-0 -z-10 bg-linear-to-t from-night/85 via-night/30 to-night/10"
        />
        <button
          type="button"
          onClick={onBack}
          className="absolute left-3 top-3 inline-flex items-center gap-1.5 rounded-full bg-white/90 px-3 py-1.5 text-xs font-semibold text-deep shadow transition hover:bg-white"
        >
          <ArrowLeft aria-hidden="true" className="h-3.5 w-3.5" />
          {t('travel.details.back')}
        </button>
        {images.length > 0 && (
          <button
            type="button"
            onClick={() => setViewing(0)}
            className="absolute right-3 top-3 inline-flex items-center gap-1.5 rounded-full bg-night/60 px-3 py-1.5 text-xs font-semibold text-white backdrop-blur transition hover:bg-night/80"
          >
            <Camera aria-hidden="true" className="h-3.5 w-3.5" />
            {t('travel.photos.all', {
              count: images.length,
              n: formatCount(images.length, language),
            })}
          </button>
        )}
        <div className="absolute inset-x-4 bottom-3 text-white">
          <h2 className="font-display text-2xl font-bold leading-tight text-white!">{name}</h2>
          <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-white/85">
            {place.division && (
              <span className="inline-flex items-center gap-1">
                <MapPin aria-hidden="true" className="h-3.5 w-3.5" />
                {t('travel.details.division', { division: t(`division.${place.division}`) })}
              </span>
            )}
            {place.kind && <span>{t(`kind.${place.kind}`)}</span>}
          </p>
        </div>
      </header>

      <div className="flex flex-col gap-5 px-5 pb-5">
        <p className="text-sm text-deep/70">
          {t('travel.details.visits', { count: visits, n: formatCount(visits, language) })}
          {' · '}
          {t('travel.details.last', { date: formatFullDate(place.lastVisitedOn, language) })}
        </p>

        <ol className="flex flex-col gap-3">
          {place.visits.map((visit) => (
            <VisitItem
              key={String(visit.id)}
              visit={visit}
              placeName={name}
              photos={place.photos.filter(
                (photo) => photo.visitId !== null && asNumber(photo.visitId) === asNumber(visit.id),
              )}
              editable={editable}
              onOpen={open}
            />
          ))}
        </ol>

        {stories.length > 0 && (
          <section aria-labelledby={`photos-${place.key}`}>
            <h3
              id={`photos-${place.key}`}
              className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-deep"
            >
              <Camera aria-hidden="true" className="h-4 w-4 text-hill" />
              {t('travel.photos.fromStories')}
            </h3>
            <ul className="grid grid-cols-3 gap-2">
              {stories.map((photo) => (
                <li key={String(photo.mediaId)}>
                  {photo.kind === 'Video' ? (
                    <a
                      href={photo.url}
                      target="_blank"
                      rel="noreferrer"
                      className="block overflow-hidden rounded-xl focus-visible:ring-2 focus-visible:ring-turmeric"
                    >
                      <video
                        src={photo.url}
                        preload="metadata"
                        muted
                        className="aspect-square w-full bg-night object-cover"
                      />
                    </a>
                  ) : (
                    <button
                      type="button"
                      onClick={() => open(photo)}
                      className="block w-full overflow-hidden rounded-xl focus-visible:ring-2 focus-visible:ring-turmeric"
                    >
                      <img
                        src={photo.url}
                        alt={t('travel.details.photoAlt', { place: name })}
                        loading="lazy"
                        className="aspect-square w-full object-cover transition duration-300 hover:scale-105"
                      />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        <div className="flex flex-wrap gap-2 border-t border-hill/10 pt-4">
          {place.destinationSlug && (
            <Link to={`/destinations/${place.destinationSlug}`} className={secondaryButtonClass}>
              <Compass aria-hidden="true" className="h-4 w-4" />
              {t('travel.details.about', { place: name })}
            </Link>
          )}
          {editable && place.destinationSlug && (
            <Link
              to={`/feed?destination=${place.destinationSlug}`}
              className={secondaryButtonClass}
            >
              <PenLine aria-hidden="true" className="h-4 w-4" />
              {t('travel.details.story')}
            </Link>
          )}
        </div>
      </div>

      {viewing !== null && (
        <PhotoViewer
          photos={viewable}
          index={Math.min(viewing, viewable.length - 1)}
          onIndex={setViewing}
          onClose={() => setViewing(null)}
        />
      )}
    </article>
  );
}

/**
 * One visit: when, how (a Ghurify trip or added by them), the trip, the note, its photos, and its
 * actions.
 */
function VisitItem({
  visit,
  placeName,
  photos,
  editable,
  onOpen,
}: {
  visit: PlaceVisit;
  placeName: string;
  /** The photos they put on this visit. */
  photos: readonly PlacePhoto[];
  editable: boolean;
  onOpen: (photo: PlacePhoto) => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const [mode, setMode] = useState<'view' | 'edit' | 'remove' | 'photos'>('view');
  const [removing, setRemoving] = useState<number | null>(null);
  const remove = useRemoveVisit();
  const removePhoto = useRemoveVisitPhoto();
  const id = asNumber(visit.id);
  const fromTrip = visit.source === 'Trip';
  const trip = visit.trip;
  const processing = asNumber(visit.photosProcessing);
  const room = maxPhotosPerVisit - photos.length - processing;

  return (
    <li className="rounded-2xl bg-mist/70 p-4 ring-1 ring-hill/10">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="flex items-center gap-1.5 text-sm font-semibold text-deep">
          <CalendarDays aria-hidden="true" className="h-4 w-4 text-hill" />
          {trip
            ? formatDateRange(trip.startDate, trip.endDate, language)
            : formatFullDate(visit.visitedOn, language)}
          {trip && <span className="font-normal text-deep/60">{trip.startDate.slice(0, 4)}</span>}
        </p>
        <span
          className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            fromTrip ? 'bg-hill text-white' : 'bg-turmeric/25 text-deep'
          }`}
        >
          {fromTrip ? t('travel.source.Trip') : t('travel.source.Added')}
        </span>
      </div>

      {trip && (
        <div className="mt-2 text-sm">
          <Link
            to={`/trips/${asNumber(trip.id)}`}
            className="font-semibold text-hill hover:underline"
          >
            {trip.title}
          </Link>
          <p className="mt-0.5 flex flex-wrap items-center gap-x-3 text-deep/70">
            <span>
              {t('travel.details.nights', {
                count: tripDays(trip.startDate, trip.endDate) - 1,
                n: formatCount(tripDays(trip.startDate, trip.endDate) - 1, language),
              })}
            </span>
            <span className="inline-flex items-center gap-1">
              {trip.asHost ? (
                <Tent aria-hidden="true" className="h-3.5 w-3.5" />
              ) : (
                <UserRound aria-hidden="true" className="h-3.5 w-3.5" />
              )}
              {trip.asHost
                ? t('travel.details.youHosted')
                : t('travel.details.hostedBy', { name: trip.hostName ?? t('chat.someone') })}
            </span>
          </p>
        </div>
      )}

      {mode === 'edit' ? (
        <VisitEditor visit={visit} onDone={() => setMode('view')} />
      ) : (
        visit.note && (
          <p className="mt-2 whitespace-pre-wrap text-sm italic text-deep/80">“{visit.note}”</p>
        )
      )}

      {photos.length > 0 && (
        <ul
          className="mt-3 grid grid-cols-4 gap-1.5"
          aria-label={t('travel.photos.ofVisit', { place: placeName })}
        >
          {photos.map((photo, index) => (
            <li key={String(photo.mediaId)} className="relative">
              <button
                type="button"
                onClick={() => onOpen(photo)}
                aria-label={t('travel.photos.open', {
                  n: formatCount(index + 1, language),
                  total: formatCount(photos.length, language),
                })}
                className="block w-full overflow-hidden rounded-lg ring-1 ring-hill/10 focus-visible:ring-2 focus-visible:ring-turmeric"
              >
                <img
                  src={photo.url}
                  alt=""
                  loading="lazy"
                  className="aspect-square w-full object-cover transition duration-300 hover:scale-105"
                />
              </button>
              {editable && (
                <button
                  type="button"
                  onClick={() => setRemoving(asNumber(photo.mediaId))}
                  aria-label={t('travel.photos.remove', { n: formatCount(index + 1, language) })}
                  className="absolute right-1 top-1 flex h-6 w-6 items-center justify-center rounded-full bg-night/70 text-white shadow transition hover:bg-jamdani"
                >
                  <X aria-hidden="true" className="h-3.5 w-3.5" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {processing > 0 && (
        <p role="status" className="mt-2 flex items-center gap-1.5 text-xs text-deep/60">
          <LoaderCircle aria-hidden="true" className="h-3.5 w-3.5 animate-spin text-hill" />
          {t('travel.photos.processing', {
            count: processing,
            n: formatCount(processing, language),
          })}
        </p>
      )}

      {removing !== null && (
        <div
          role="group"
          aria-label={t('travel.photos.removeTitle')}
          className="mt-3 flex flex-wrap items-center justify-between gap-2 rounded-xl bg-white p-3 text-sm"
        >
          <p className="font-semibold text-deep">{t('travel.photos.removeTitle')}</p>
          {removePhoto.isError && (
            <p role="alert" className="w-full text-jamdani">
              {errorText(removePhoto.error, t)}
            </p>
          )}
          <div className="flex gap-2">
            <button
              type="button"
              className={secondaryButtonClass}
              onClick={() => setRemoving(null)}
            >
              {t('travel.photos.keep')}
            </button>
            <button
              type="button"
              className={dangerButtonClass}
              disabled={removePhoto.isPending}
              onClick={() =>
                removePhoto.mutate(
                  { visitId: id, mediaId: removing },
                  { onSuccess: () => setRemoving(null) },
                )
              }
            >
              {t('travel.photos.removeConfirm')}
            </button>
          </div>
        </div>
      )}

      {mode === 'photos' && (
        <VisitPhotosEditor visitId={id} room={room} onDone={() => setMode('view')} />
      )}

      {editable && mode === 'view' && (
        <div className="mt-3 flex flex-wrap gap-1">
          {room > 0 && (
            <button
              type="button"
              onClick={() => setMode('photos')}
              className="inline-flex items-center gap-1 rounded-full px-2.5 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-white hover:text-hill"
            >
              <ImagePlus aria-hidden="true" className="h-3.5 w-3.5" />
              {t('travel.photos.add')}
            </button>
          )}
          <button
            type="button"
            onClick={() => setMode('edit')}
            className="inline-flex items-center gap-1 rounded-full px-2.5 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-white hover:text-hill"
          >
            <Pencil aria-hidden="true" className="h-3.5 w-3.5" />
            {visit.note ? t('travel.details.editNote') : t('travel.details.addNote')}
          </button>
          <button
            type="button"
            onClick={() => setMode('remove')}
            className="inline-flex items-center gap-1 rounded-full px-2.5 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-jamdani/10 hover:text-jamdani"
          >
            <Trash2 aria-hidden="true" className="h-3.5 w-3.5" />
            {t('travel.details.remove')}
          </button>
        </div>
      )}

      {mode === 'remove' && (
        <div
          role="group"
          aria-label={t('travel.details.removeTitle')}
          className="mt-3 rounded-xl bg-white p-3 text-sm"
        >
          <p className="font-semibold text-deep">{t('travel.details.removeTitle')}</p>
          <p className="mt-0.5 text-deep/70">
            {fromTrip ? t('travel.details.removeTrip') : t('travel.details.removeAdded')}
          </p>
          {remove.isError && (
            <p role="alert" className="mt-1 text-jamdani">
              {errorText(remove.error, t)}
            </p>
          )}
          <div className="mt-2 flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setMode('view')}>
              {t('common.cancel')}
            </button>
            <button
              type="button"
              className={dangerButtonClass}
              disabled={remove.isPending}
              onClick={() => remove.mutate(id)}
            >
              {t('travel.details.removeConfirm')}
            </button>
          </div>
        </div>
      )}
    </li>
  );
}

/** Adding photos to a visit already on the map: choose, wait for the uploads, save. */
function VisitPhotosEditor({
  visitId,
  room,
  onDone,
}: {
  visitId: number;
  /** How many more the visit can take. */
  room: number;
  onDone: () => void;
}) {
  const { t } = useTranslation();
  const photos = usePhotoUploads(room);
  const save = useAddVisitPhotos();

  return (
    <div className="mt-3 flex flex-col gap-3 rounded-xl bg-white p-3">
      <PhotoPicker
        uploads={photos.uploads}
        limit={room}
        error={photos.error}
        onAdd={photos.add}
        onRemove={photos.remove}
      />
      {save.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(save.error, t)}
        </p>
      )}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          className={secondaryButtonClass}
          onClick={() => {
            photos.reset();
            onDone();
          }}
        >
          {t('common.cancel')}
        </button>
        <button
          type="button"
          className={primaryButtonClass}
          disabled={photos.uploading || photos.mediaIds.length === 0 || save.isPending}
          onClick={() =>
            save.mutate(
              { visitId, mediaIds: photos.mediaIds },
              {
                onSuccess: () => {
                  photos.reset();
                  onDone();
                },
              },
            )
          }
        >
          {photos.uploading
            ? t('travel.photos.uploading')
            : save.isPending
              ? t('common.saving')
              : t('travel.photos.save')}
        </button>
      </div>
    </div>
  );
}

/** The note, and the date of a visit they added (a trip's date is the trip's). */
function VisitEditor({ visit, onDone }: { visit: PlaceVisit; onDone: () => void }) {
  const { t } = useTranslation();
  const edit = useEditVisit();
  const [note, setNote] = useState(visit.note ?? '');
  const [visitedOn, setVisitedOn] = useState(visit.visitedOn);
  const id = asNumber(visit.id);
  const added = visit.source === 'Added';

  return (
    <form
      className="mt-3 flex flex-col gap-3"
      onSubmit={(event) => {
        event.preventDefault();
        edit.mutate(
          { id, command: { visitedOn: added ? visitedOn : null, note: note.trim() || null } },
          { onSuccess: onDone },
        );
      }}
    >
      {added && (
        <DateField
          id={`visit-${id}-date`}
          label={t('travel.add.date')}
          value={visitedOn}
          onChange={setVisitedOn}
          max={todayInDhaka()}
          min="1950-01-01"
        />
      )}
      <TextAreaField
        id={`visit-${id}-note`}
        label={t('travel.add.note')}
        rows={2}
        maxLength={500}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />
      {edit.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(edit.error, t)}
        </p>
      )}
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButtonClass} onClick={onDone}>
          {t('common.cancel')}
        </button>
        <button
          type="submit"
          className={primaryButtonClass}
          disabled={edit.isPending || !visitedOn}
        >
          {edit.isPending ? t('common.saving') : t('travel.details.save')}
        </button>
      </div>
    </form>
  );
}

import { useEffect, useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight, MapPin, X } from 'lucide-react';

import { formatCount, toLanguage } from '@/lib/format';

export interface ViewedPhoto {
  key: string;
  url: string;
  alt: string;
  /** Where and when, under the photo. */
  place: string;
  when: string | null;
}

/**
 * A place's photos, one at a time and large, with where and when they were taken. Arrow keys and
 * the buttons move between them; Escape closes. Built on the native dialog, as Dialog is.
 */
export function PhotoViewer({
  photos,
  index,
  onIndex,
  onClose,
}: {
  photos: readonly ViewedPhoto[];
  index: number;
  onIndex: (index: number) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const photo = photos[index];
  const many = photos.length > 1;
  const step = (by: number) => onIndex((index + by + photos.length) % photos.length);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog || dialog.open) return;
    if (typeof dialog.showModal === 'function') dialog.showModal();
    else dialog.setAttribute('open', '');
  }, []);

  if (!photo) return null;

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={onClose}
      onKeyDown={(event) => {
        if (!many) return;
        if (event.key === 'ArrowRight') step(1);
        if (event.key === 'ArrowLeft') step(-1);
      }}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[min(60rem,calc(100vw-2rem))] overflow-hidden rounded-3xl bg-night p-0 text-white shadow-2xl backdrop:bg-night/80 backdrop:backdrop-blur-sm"
    >
      <figure className="flex flex-col">
        <div className="relative flex items-center justify-center bg-black">
          <img
            src={photo.url}
            alt={photo.alt}
            className="max-h-[calc(100dvh-10rem)] w-full object-contain"
          />
          {many && (
            <>
              <button
                type="button"
                onClick={() => step(-1)}
                aria-label={t('travel.photos.previous')}
                className="absolute left-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 text-deep shadow transition hover:bg-white"
              >
                <ChevronLeft aria-hidden="true" className="h-5 w-5" />
              </button>
              <button
                type="button"
                onClick={() => step(1)}
                aria-label={t('travel.photos.next')}
                className="absolute right-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 text-deep shadow transition hover:bg-white"
              >
                <ChevronRight aria-hidden="true" className="h-5 w-5" />
              </button>
            </>
          )}
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close')}
            className="absolute right-3 top-3 flex h-9 w-9 items-center justify-center rounded-full bg-night/70 text-white transition hover:bg-night"
          >
            <X aria-hidden="true" className="h-5 w-5" />
          </button>
        </div>
        <figcaption className="flex items-center justify-between gap-4 px-5 py-4">
          <div className="min-w-0">
            <p
              id={titleId}
              className="flex items-center gap-1.5 truncate font-display font-semibold"
            >
              <MapPin aria-hidden="true" className="h-4 w-4 shrink-0 text-dusk" />
              {photo.place}
            </p>
            {photo.when && <p className="mt-0.5 text-sm text-white/70">{photo.when}</p>}
          </div>
          {many && (
            <p className="shrink-0 text-sm text-white/70">
              {t('travel.photos.counter', {
                n: formatCount(index + 1, language),
                total: formatCount(photos.length, language),
              })}
            </p>
          )}
        </figcaption>
      </figure>
    </dialog>
  );
}

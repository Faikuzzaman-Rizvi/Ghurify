import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, Check, ImagePlus, X } from 'lucide-react';

import { formatCount, toLanguage } from '@/lib/format';
import type { PhotoUpload } from './usePhotoUploads';

/**
 * Choosing photos for a place: a tile per photo (its preview, upload progress, and a way to drop
 * it) and a tile to add more. The input takes JPEG, PNG and WebP, several at once; on a phone it
 * offers the camera too.
 */
export function PhotoPicker({
  uploads,
  limit,
  error,
  onAdd,
  onRemove,
}: {
  uploads: readonly PhotoUpload[];
  /** How many more this place can take, counting these. */
  limit: number;
  error: string | null;
  onAdd: (files: FileList | null) => void;
  onRemove: (key: number) => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const inputId = useId();
  const hintId = useId();
  const kept = uploads.filter((item) => !item.failed).length;

  return (
    <div className="flex flex-col gap-2">
      <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
        {uploads.map((item) => {
          const done = item.mediaId !== undefined;
          return (
            <li
              key={item.key}
              className="group relative aspect-square overflow-hidden rounded-xl bg-mist ring-1 ring-hill/10"
            >
              {item.preview && (
                <img src={item.preview} alt="" className="h-full w-full object-cover" />
              )}
              {!done && !item.failed && (
                <div className="absolute inset-0 flex flex-col justify-end bg-night/45 p-2">
                  <progress
                    max={1}
                    value={item.progress}
                    aria-label={t('travel.photos.progress', { name: item.name })}
                    className="h-1.5 w-full overflow-hidden rounded-full [&::-moz-progress-bar]:bg-turmeric [&::-webkit-progress-bar]:bg-white/40 [&::-webkit-progress-value]:bg-turmeric"
                  />
                </div>
              )}
              {item.failed && (
                <div className="absolute inset-0 flex flex-col items-center justify-center gap-1 bg-jamdani/80 p-2 text-center text-[0.7rem] font-semibold text-white">
                  <AlertCircle aria-hidden="true" className="h-5 w-5" />
                  {t('travel.photos.failed')}
                </div>
              )}
              {done && (
                <span
                  aria-hidden="true"
                  className="absolute bottom-1.5 left-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-hill text-white shadow"
                >
                  <Check className="h-3 w-3" />
                </span>
              )}
              <button
                type="button"
                onClick={() => onRemove(item.key)}
                aria-label={t('travel.photos.drop', { name: item.name })}
                className="absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-full bg-night/70 text-white shadow transition hover:bg-night"
              >
                <X aria-hidden="true" className="h-3.5 w-3.5" />
              </button>
            </li>
          );
        })}

        {kept < limit && (
          <li className="aspect-square">
            <label
              htmlFor={inputId}
              className="flex h-full w-full cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-hill/30 bg-mist/50 p-2 text-center text-xs font-semibold text-hill transition hover:border-hill hover:bg-hill/5 has-focus-visible:ring-2 has-focus-visible:ring-turmeric"
            >
              <ImagePlus aria-hidden="true" className="h-6 w-6" />
              {t('travel.photos.add')}
              <span className="font-normal text-deep/50">
                {t('travel.photos.count', {
                  n: formatCount(kept, language),
                  max: formatCount(limit, language),
                })}
              </span>
              <input
                id={inputId}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                multiple
                aria-label={t('travel.photos.add')}
                aria-describedby={hintId}
                className="sr-only"
                onChange={(event) => {
                  onAdd(event.target.files);
                  // The same file can be chosen again after it was dropped.
                  event.target.value = '';
                }}
              />
            </label>
          </li>
        )}
      </ul>
      <p id={hintId} className="text-xs text-deep/60">
        {t('travel.photos.hint')}
      </p>
      {error && (
        <p role="alert" className="text-xs text-jamdani">
          {error}
        </p>
      )}
    </div>
  );
}

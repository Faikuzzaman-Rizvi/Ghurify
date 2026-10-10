import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { MapPin, X } from 'lucide-react';

import { asNumber } from '@/api/client';
import { inputClass, primaryButtonClass, secondaryButtonClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { feedApi, type PostView } from './feedApi';

/**
 * Editing one's own story in place: the text, the destination, and which of its photos stay.
 * Adding photos is a new story; this keeps the editor simple and the post's comments in context.
 */
export function PostEditor({ post, onDone }: { post: PostView; onDone: () => void }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const { data: destinations } = useDestinations();
  const id = asNumber(post.id);
  const [body, setBody] = useState(post.body);
  const [destination, setDestination] = useState(post.destinationSlug ?? '');
  const [keep, setKeep] = useState(() => post.media.map((item) => asNumber(item.id)));
  const empty = body.trim().length === 0 && keep.length === 0;

  const save = useMutation({
    mutationFn: () =>
      feedApi.editPost(id, {
        body: body.trim(),
        destinationSlug: destination || null,
        keepMediaIds: keep,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['feed'] });
      onDone();
    },
  });

  return (
    <form
      className="flex flex-col gap-3"
      aria-label={t('feed.edit.title')}
      onSubmit={(event) => {
        event.preventDefault();
        if (!empty) save.mutate();
      }}
    >
      <label htmlFor={`post-body-${id}`} className="sr-only">
        {t('feed.edit.text')}
      </label>
      <textarea
        id={`post-body-${id}`}
        rows={4}
        maxLength={2000}
        value={body}
        onChange={(event) => setBody(event.target.value)}
        className={`${inputClass} resize-y`}
        autoFocus
      />

      {post.media.length > 0 && (
        <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4" aria-label={t('feed.edit.photos')}>
          {post.media.map((item) => {
            const mediaId = asNumber(item.id);
            const kept = keep.includes(mediaId);
            return (
              <li key={mediaId} className="relative">
                {item.kind === 'Video' ? (
                  <video
                    src={item.url}
                    className={`aspect-square w-full rounded-lg bg-night object-cover ${kept ? '' : 'opacity-30'}`}
                  />
                ) : (
                  <img
                    src={item.url}
                    alt=""
                    className={`aspect-square w-full rounded-lg object-cover ${kept ? '' : 'opacity-30 grayscale'}`}
                  />
                )}
                <button
                  type="button"
                  aria-pressed={!kept}
                  onClick={() =>
                    setKeep((current) =>
                      kept ? current.filter((value) => value !== mediaId) : [...current, mediaId],
                    )
                  }
                  className="absolute right-1 top-1 inline-flex items-center gap-1 rounded-full bg-white/90 px-2 py-1 text-xs font-semibold text-deep shadow"
                >
                  {kept ? (
                    <>
                      <X aria-hidden="true" className="h-3 w-3" />
                      {t('feed.edit.removePhoto')}
                    </>
                  ) : (
                    t('feed.edit.keepPhoto')
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}

      <div className="flex flex-col gap-1">
        <label
          htmlFor={`post-destination-${id}`}
          className="flex items-center gap-1 text-xs font-semibold text-deep/70"
        >
          <MapPin aria-hidden="true" className="h-3.5 w-3.5 text-ochre" />
          {t('feed.composer.destination')}
        </label>
        <Select
          id={`post-destination-${id}`}
          value={destination}
          onChange={setDestination}
          options={[
            { value: '', label: t('feed.composer.noDestination') },
            ...(destinations ?? []).map((item) => ({
              value: item.slug,
              label: language === 'bn' ? item.nameBn : item.name,
            })),
          ]}
          buttonClassName={`${inputClass} cursor-pointer py-2.5`}
        />
      </div>

      {empty && (
        <p role="status" className="text-sm text-deep/70">
          {t('feed.edit.empty')}
        </p>
      )}
      {save.isError && (
        <p role="alert" className="text-sm text-jamdani">
          {errorText(save.error, t)}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButtonClass} onClick={onDone}>
          {t('common.cancel')}
        </button>
        <button type="submit" className={primaryButtonClass} disabled={empty || save.isPending}>
          {save.isPending ? t('common.saving') : t('feed.edit.save')}
        </button>
      </div>
    </form>
  );
}

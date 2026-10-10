import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ImagePlus, MapPin, ShieldCheck } from 'lucide-react';

import { asNumber } from '@/api/client';
import { cardClass, inputClass, primaryButtonClass } from '@/components/Field';
import { Select } from '@/components/ui/Select';
import { useAuthStore } from '@/features/auth/authStore';
import { useDestinations } from '@/features/trips/useTrips';
import { errorText } from '@/lib/errors';
import { toLanguage } from '@/lib/format';
import { feedApi, uploadToStorage } from './feedApi';
import { Avatar } from './PostCard';

const accept = 'image/jpeg,image/png,image/webp,video/mp4,video/quicktime';

interface Upload {
  name: string;
  progress: number;
  mediaId?: number;
  failed?: boolean;
}

/**
 * Write a story and add photos or videos. Files go straight to storage with a short link, with a
 * progress bar each; the server then strips location data before anyone else sees them.
 */
/** `initialDestination`: a story about a place, started from that place (the travel map). */
export function StoryComposer({ initialDestination = '' }: { initialDestination?: string }) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const queryClient = useQueryClient();
  const { data: destinations } = useDestinations();
  const name = useAuthStore((state) => state.user?.displayName ?? null);
  const userId = useAuthStore((state) => state.user?.id ?? null);
  const [body, setBody] = useState('');
  const [destination, setDestination] = useState(initialDestination);
  const [uploads, setUploads] = useState<Upload[]>([]);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const uploading = uploads.some((upload) => upload.mediaId === undefined && !upload.failed);
  const mediaIds = uploads.flatMap((upload) => (upload.mediaId ? [upload.mediaId] : []));

  async function addFiles(files: FileList | null) {
    setUploadError(null);
    for (const file of Array.from(files ?? []).slice(0, 10 - uploads.length)) {
      const name = file.name;
      setUploads((current) => [...current, { name, progress: 0 }]);
      try {
        const link = await feedApi.uploadLink(file.type, file.size);
        await uploadToStorage(link.uploadUrl, file, (progress) =>
          setUploads((current) =>
            current.map((upload) => (upload.name === name ? { ...upload, progress } : upload)),
          ),
        );
        await feedApi.completeUpload(asNumber(link.mediaId));
        setUploads((current) =>
          current.map((upload) =>
            upload.name === name
              ? { ...upload, progress: 1, mediaId: asNumber(link.mediaId) }
              : upload,
          ),
        );
      } catch (error) {
        setUploadError(errorText(error, t));
        setUploads((current) =>
          current.map((upload) => (upload.name === name ? { ...upload, failed: true } : upload)),
        );
      }
    }
  }

  const post = useMutation({
    mutationFn: () =>
      feedApi.createPost({
        body: body.trim() || null,
        destinationSlug: destination || null,
        tripId: null,
        mediaIds,
      }),
    onSuccess: () => {
      setBody('');
      setDestination('');
      setUploads([]);
      void queryClient.invalidateQueries({ queryKey: ['feed'] });
    },
  });

  return (
    <form
      className={`${cardClass} flex flex-col gap-4`}
      onSubmit={(event) => {
        event.preventDefault();
        post.mutate();
      }}
    >
      <div className="flex gap-3">
        <Avatar name={name} userId={userId} />
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <label htmlFor="story-body" className="font-display font-semibold text-deep">
            {t('feed.composer.label')}
          </label>
          <textarea
            id="story-body"
            rows={3}
            maxLength={2000}
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder={t('feed.composer.placeholder')}
            className={`${inputClass} resize-y bg-mist/60`}
          />
        </div>
      </div>

      {uploads.length > 0 && (
        <ul className="flex flex-col gap-2 sm:pl-14" aria-label={t('feed.composer.uploads')}>
          {uploads.map((upload) => (
            <li key={upload.name} className="rounded-lg bg-mist px-3 py-2 text-xs text-deep/80">
              <span className="block truncate">{upload.name}</span>
              <progress
                className="mt-1 h-1.5 w-full accent-hill"
                value={upload.failed ? 0 : upload.progress}
                max={1}
                aria-label={t('feed.composer.progress', { name: upload.name })}
              />
            </li>
          ))}
        </ul>
      )}

      <div className="flex flex-wrap items-end gap-3 border-t border-hill/10 pt-4 sm:pl-14">
        <div className="flex min-w-48 flex-1 flex-col gap-1">
          <label
            htmlFor="story-destination"
            className="flex items-center gap-1 text-xs font-semibold text-deep/70"
          >
            <MapPin aria-hidden="true" className="h-3.5 w-3.5 text-ochre" />
            {t('feed.composer.destination')}
          </label>
          <Select
            id="story-destination"
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
        <label className="inline-flex cursor-pointer items-center gap-2 rounded-full border border-hill/20 px-4 py-2.5 text-sm font-semibold text-deep transition focus-within:ring-2 focus-within:ring-turmeric hover:bg-mist">
          <ImagePlus aria-hidden="true" className="h-4 w-4 text-hill" />
          {t('feed.composer.addMedia')}
          <input
            type="file"
            accept={accept}
            multiple
            className="sr-only"
            onChange={(event) => void addFiles(event.target.files)}
          />
        </label>
        <button
          type="submit"
          className={primaryButtonClass}
          disabled={post.isPending || uploading || (!body.trim() && mediaIds.length === 0)}
        >
          {t('feed.composer.post')}
        </button>
      </div>

      <p className="flex items-center gap-1.5 text-xs text-deep/60 sm:pl-14">
        <ShieldCheck aria-hidden="true" className="h-3.5 w-3.5 text-hill" />
        {t('feed.composer.privacy')}
      </p>

      {(uploadError || post.isError) && (
        <p role="alert" className="text-sm text-jamdani">
          {uploadError ?? errorText(post.error, t)}
        </p>
      )}
    </form>
  );
}

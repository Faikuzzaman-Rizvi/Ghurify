import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { asNumber } from '@/api/client';
import { feedApi, uploadToStorage } from '@/features/feed/feedApi';
import { errorText } from '@/lib/errors';

/** One photo on its way: a local preview, how far along, and its id once it is uploaded. */
export interface PhotoUpload {
  key: number;
  name: string;
  /** A local link to the chosen file, for the thumbnail; empty where the browser has none. */
  preview: string;
  progress: number;
  mediaId?: number;
  failed?: boolean;
}

const accepted = ['image/jpeg', 'image/png', 'image/webp'];

const previewOf = (file: File) =>
  typeof URL.createObjectURL === 'function' ? URL.createObjectURL(file) : '';
const release = (preview: string) => {
  if (preview && typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(preview);
};

/**
 * Travel photos, uploaded the way story photos are: a short-lived link, the file straight to
 * storage, then the server checks it and strips its location data. Each starts as soon as it is
 * chosen, so by the time the form is sent only the ids are left to send.
 */
export function usePhotoUploads(limit: number) {
  const { t } = useTranslation();
  const [uploads, setUploads] = useState<PhotoUpload[]>([]);
  const [error, setError] = useState<string | null>(null);
  const next = useRef(0);
  const previews = useRef(new Set<string>());

  useEffect(() => {
    const live = previews.current;
    return () => live.forEach(release);
  }, []);

  const update = (key: number, change: Partial<PhotoUpload>) =>
    setUploads((current) =>
      current.map((upload) => (upload.key === key ? { ...upload, ...change } : upload)),
    );

  const upload = async (key: number, file: File) => {
    try {
      const link = await feedApi.uploadLink(file.type, file.size);
      await uploadToStorage(link.uploadUrl, file, (progress) => update(key, { progress }));
      const mediaId = asNumber(link.mediaId);
      await feedApi.completeUpload(mediaId);
      update(key, { progress: 1, mediaId });
    } catch (failure) {
      setError(errorText(failure, t));
      update(key, { failed: true });
    }
  };

  const add = (files: FileList | readonly File[] | null) => {
    setError(null);
    const chosen = Array.from(files ?? []);
    const images = chosen.filter((file) => accepted.includes(file.type));
    const room = Math.max(0, limit - uploads.filter((item) => !item.failed).length);

    if (images.length < chosen.length) setError(t('travel.photos.onlyImages'));
    if (images.length > room) setError(t('travel.photos.tooMany', { max: limit }));

    for (const file of images.slice(0, room)) {
      const key = next.current++;
      const preview = previewOf(file);
      if (preview) previews.current.add(preview);
      setUploads((current) => [...current, { key, name: file.name, preview, progress: 0 }]);
      void upload(key, file);
    }
  };

  const remove = (key: number) =>
    setUploads((current) =>
      current.filter((item) => {
        if (item.key === key) release(item.preview);
        return item.key !== key;
      }),
    );

  const reset = () => {
    uploads.forEach((item) => release(item.preview));
    setUploads([]);
    setError(null);
  };

  return {
    uploads,
    error,
    uploading: uploads.some((item) => item.mediaId === undefined && !item.failed),
    mediaIds: uploads.flatMap((item) => (item.mediaId === undefined ? [] : [item.mediaId])),
    add,
    remove,
    reset,
  };
}

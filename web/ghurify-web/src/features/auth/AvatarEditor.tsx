import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Camera, Trash2 } from 'lucide-react';

import { asNumber } from '@/api/client';
import { Avatar } from '@/components/Avatar';
import { errorText } from '@/lib/errors';
import { acceptedImageTypes } from '@/lib/images';
import type { Profile } from './profileApi';
import { useRemoveAvatar, useUploadAvatar } from './useProfile';

/**
 * The profile picture with change and remove buttons. The photo is cropped square and shrunk in
 * the browser, so even a large phone photo uploads in a moment.
 */
export function AvatarEditor({ profile }: { profile: Profile }) {
  const { t } = useTranslation();
  const upload = useUploadAvatar();
  const remove = useRemoveAvatar();
  const input = useRef<HTMLInputElement>(null);
  const hasAvatar = profile.avatarVersion !== null && profile.avatarVersion !== undefined;
  const busy = upload.isPending || remove.isPending;

  return (
    <div className="flex flex-col items-center gap-2">
      <div className="relative">
        <Avatar
          userId={asNumber(profile.userId)}
          name={profile.displayName ?? profile.maskedEmail}
          version={profile.avatarVersion}
          size="xl"
          label={t('account.avatar.label')}
          className="ring-8 ring-mist"
        />
        <button
          type="button"
          onClick={() => input.current?.click()}
          disabled={busy}
          aria-label={hasAvatar ? t('account.avatar.change') : t('account.avatar.add')}
          className="absolute bottom-0 right-0 flex h-9 w-9 items-center justify-center rounded-full bg-turmeric text-deep shadow-md ring-2 ring-white transition hover:bg-dusk disabled:opacity-60"
        >
          <Camera aria-hidden="true" className="h-4 w-4" />
        </button>
      </div>

      <input
        ref={input}
        type="file"
        accept={acceptedImageTypes}
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = '';
          if (file) upload.mutate(file);
        }}
      />

      {busy && (
        <p role="status" className="text-xs text-deep/70">
          {t('common.saving')}
        </p>
      )}
      {hasAvatar && !busy && (
        <button
          type="button"
          onClick={() => remove.mutate()}
          className="inline-flex items-center gap-1 text-xs text-deep/60 underline hover:text-jamdani"
        >
          <Trash2 aria-hidden="true" className="h-3.5 w-3.5" />
          {t('account.avatar.remove')}
        </button>
      )}
      {(upload.isError || remove.isError) && (
        <p role="alert" className="text-xs text-jamdani">
          {errorText(upload.error ?? remove.error, t)}
        </p>
      )}
    </div>
  );
}

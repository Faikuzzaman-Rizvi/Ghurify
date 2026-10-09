import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ImageUp, RotateCcw } from 'lucide-react';

import { siteConfigKey } from '@/features/site/useSiteConfig';
import { prepareImage } from '@/lib/images';
import { errorText } from '@/lib/errors';
import { adminApi, type SiteAssetView } from './adminApi';

/** The longest side each kind is shrunk to in the browser before it is sent. */
const maxSide: Record<string, number> = {
  logo: 512,
  'logo-dark': 512,
  favicon: 256,
  'apple-touch-icon': 180,
  'social-image': 1200,
};

/**
 * One of the site's images: what it is now, and how to replace it or put it back.
 *
 * The file is shrunk and re-encoded in the browser first, with the same helper the profile
 * pictures use. That keeps the upload small, and it means the bytes the API receives were
 * produced by the canvas rather than taken from the file — so a crafted image cannot survive
 * the round trip even before the API checks it again.
 */
export function SiteAssetField({
  asset,
  disabled,
}: {
  asset: SiteAssetView;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['admin', 'settings'] });
    void queryClient.invalidateQueries({ queryKey: siteConfigKey });
  };

  const upload = useMutation({
    mutationFn: async (file: File) => {
      const shrunk = await prepareImage(file, { maxSide: maxSide[asset.kind] ?? 512 });
      const base64 = await toBase64(shrunk);
      await adminApi.uploadSiteAsset(asset.kind, shrunk.type, base64);
    },
    onSuccess: refresh,
    onError: (error) => setProblem(errorText(error, t)),
  });

  const remove = useMutation({
    mutationFn: () => adminApi.removeSiteAsset(asset.kind),
    onSuccess: refresh,
    onError: (error) => setProblem(errorText(error, t)),
  });

  const busy = upload.isPending || remove.isPending;

  return (
    <div className="flex flex-col gap-2 rounded-xl border border-hill/15 p-3">
      <p className="text-sm font-semibold text-deep">
        {t(`admin.assets.${asset.kind}`, { defaultValue: asset.kind })}
      </p>

      {/* A chequered backdrop, so a transparent logo is visibly transparent rather than
          looking like it has a white box around it. */}
      <div className="flex h-24 items-center justify-center rounded-lg bg-[repeating-conic-gradient(var(--color-mist)_0_25%,white_0_50%)] bg-[length:16px_16px] p-2">
        {asset.url.length > 0 ? (
          <img
            src={asset.url}
            alt={t(`admin.assets.${asset.kind}`, { defaultValue: asset.kind })}
            className="max-h-full max-w-full object-contain"
          />
        ) : (
          <p className="text-xs text-deep/50">{t('admin.assets.none')}</p>
        )}
      </div>

      <p className="text-xs text-deep/60">
        {asset.isCustom
          ? t('admin.assets.uploaded', { kb: Math.round(Number(asset.sizeBytes ?? 0) / 1024) })
          : t('admin.assets.builtIn')}
      </p>

      {problem && (
        <p role="alert" className="text-xs font-medium text-jamdani">
          {problem}
        </p>
      )}

      <div className="mt-auto flex flex-wrap gap-1.5">
        <input
          ref={input}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          className="sr-only"
          onChange={(event) => {
            const file = event.target.files?.[0];
            event.target.value = '';
            if (file) {
              setProblem(null);
              upload.mutate(file);
            }
          }}
        />
        <button
          type="button"
          disabled={disabled || busy}
          onClick={() => input.current?.click()}
          className="flex items-center gap-1.5 rounded-full bg-hill px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-deep disabled:opacity-60"
        >
          <ImageUp aria-hidden="true" className="h-3.5 w-3.5" />
          {upload.isPending ? t('common.saving') : t('admin.assets.replace')}
        </button>

        {asset.isCustom && (
          <button
            type="button"
            disabled={disabled || busy}
            onClick={() => {
              setProblem(null);
              remove.mutate();
            }}
            className="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold text-deep/70 transition hover:bg-mist hover:text-deep disabled:opacity-60"
          >
            <RotateCcw aria-hidden="true" className="h-3.5 w-3.5" />
            {t('admin.assets.reset')}
          </button>
        )}
      </div>
    </div>
  );
}

/** The blob's bytes as base64, without the data-URL prefix the API has no use for. */
function toBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('The file could not be read.'));
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('The file could not be read.'));
        return;
      }
      // Drops the "data:image/png;base64," prefix, which the API has no use for.
      resolve(result.slice(result.indexOf(',') + 1));
    };
    reader.readAsDataURL(blob);
  });
}

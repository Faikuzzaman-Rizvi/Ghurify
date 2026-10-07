import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Scenery } from '@/components/Scenery';
import type { DestinationKind } from '@/features/trips/tripsApi';
import { hasOwnPhoto, photoFor, photoSources, type PhotoCut } from '@/lib/photos';

interface PhotoProps {
  slug: string;
  kind: DestinationKind;
  cut: PhotoCut;
  className?: string;
  /** Extra classes for the <img> itself, e.g. the hover zoom. */
  imgClassName?: string;
  /** Above the fold: load now, at high priority, instead of lazily. */
  priority?: boolean;
  /** The displayed width, so the browser picks the right file from the srcset. */
  sizes?: string;
  /** True when the place name is already written next to the photo. */
  decorative?: boolean;
}

/**
 * A destination photo that fills its box. The photo's average colour shows while it loads and
 * it fades in once decoded; if it fails (offline, blocked), the illustrated scenery for the
 * kind stands in, so a card never shows a broken image.
 */
export function Photo({
  slug,
  kind,
  cut,
  className = '',
  imgClassName = '',
  priority = false,
  sizes = '100vw',
  decorative = false,
}: PhotoProps) {
  const { t } = useTranslation();
  const photo = photoFor(slug, kind);
  const { src, srcSet } = photoSources(photo, cut);
  const [state, setState] = useState<'loading' | 'loaded' | 'failed'>('loading');

  // A borrowed photo shows another place: describing it would mislead.
  const alt = decorative || !hasOwnPhoto(slug) ? '' : t(`photo.${slug}`);

  return (
    <div
      className={`relative overflow-hidden ${className}`}
      style={{ backgroundColor: photo.color }}
    >
      {state === 'failed' ? (
        <Scenery kind={kind} className="absolute inset-0 h-full w-full" />
      ) : (
        <img
          src={src}
          srcSet={srcSet}
          sizes={srcSet ? sizes : undefined}
          alt={alt}
          loading={priority ? 'eager' : 'lazy'}
          fetchPriority={priority ? 'high' : 'auto'}
          decoding="async"
          onLoad={() => setState('loaded')}
          onError={() => setState('failed')}
          className={`absolute inset-0 h-full w-full object-cover transition-[opacity,transform,filter] duration-700 ease-out ${
            state === 'loaded' ? 'opacity-100' : 'opacity-0'
          } ${imgClassName}`}
        />
      )}
    </div>
  );
}

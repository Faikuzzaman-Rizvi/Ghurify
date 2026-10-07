import { useState } from 'react';
import { avatarUrl } from '@/features/auth/profileApi';

const sizes = {
  sm: 'h-8 w-8 text-sm',
  md: 'h-12 w-12 text-lg',
  lg: 'h-20 w-20 text-3xl',
  xl: 'h-28 w-28 text-4xl',
} as const;

/**
 * Someone's profile picture, or their initial on a hill-green circle when they have none (the
 * API answers 404 and the image quietly gives way). Decorative next to a name, so no alt text
 * repeats it; pass `label` where the picture stands alone.
 */
export function Avatar({
  userId,
  name,
  version,
  size = 'md',
  label,
  className = '',
}: {
  userId: number | string;
  name: string | null | undefined;
  version?: number | string | null | undefined;
  size?: keyof typeof sizes;
  label?: string;
  className?: string;
}) {
  const src = avatarUrl(userId, version);
  const [failed, setFailed] = useState<string | null>(null);
  const initial = (name?.trim() || 'G').charAt(0).toUpperCase();
  const box = `${sizes[size]} shrink-0 rounded-full ${className}`;

  if (failed === src) {
    return (
      <span
        role={label ? 'img' : undefined}
        aria-label={label}
        aria-hidden={label ? undefined : true}
        className={`${box} flex items-center justify-center bg-hill font-display font-bold text-white`}
      >
        {initial}
      </span>
    );
  }

  return (
    <img
      src={src}
      alt={label ?? ''}
      loading="lazy"
      onError={() => setFailed(src)}
      className={`${box} bg-mist object-cover`}
    />
  );
}

import type { ReactNode } from 'react';
import type { DestinationKind } from '@/features/trips/tripsApi';
import { useHeroUnderHeader } from './headerStore';
import { Photo } from './Photo';
import { AccentTitle } from './SectionHeading';

/**
 * The photo band at the top of an inner page: eyebrow and a two-tone title over a darkened
 * photo, with room above for the floating header. Children go under the title (breadcrumbs,
 * badges, facts).
 */
export function PageBanner({
  slug,
  kind,
  eyebrow,
  titleKey,
  title,
  children,
  tall = false,
  compact = false,
  aside,
}: {
  slug: string;
  kind: DestinationKind;
  eyebrow?: ReactNode;
  /** A translation key with an <accent> part, for fixed titles. */
  titleKey?: string;
  /** Plain text, for titles that come from data (a trip's name). */
  title?: ReactNode;
  children?: ReactNode;
  tall?: boolean;
  /** Dashboard pages: a shorter band with room at the foot for cards to overlap it. */
  compact?: boolean;
  /** Sits at the right of the title on wide screens: the page's main action. */
  aside?: ReactNode;
}) {
  useHeroUnderHeader();

  return (
    <header className="relative isolate overflow-hidden bg-night text-white">
      <Photo
        slug={slug}
        kind={kind}
        cut="wide"
        priority
        className="absolute! inset-0 -z-10"
        imgClassName="animate-ken-burns"
      />
      {/* Darker at the bottom-left, where the text sits, so it reads on any photo. */}
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-linear-to-tr from-night/90 via-night/55 to-night/25"
      />

      <div
        className={`container-page flex flex-wrap items-end justify-between gap-x-8 gap-y-6 ${
          compact
            ? 'min-h-60 pb-20 pt-26 sm:min-h-68'
            : tall
              ? 'min-h-120 pb-16 pt-32 sm:min-h-144'
              : 'min-h-80 pb-12 pt-30 sm:min-h-96'
        }`}
      >
        <div className="max-w-3xl animate-rise">
          {eyebrow && <div className="eyebrow text-dusk!">{eyebrow}</div>}
          <h1
            className={`mt-3 font-bold uppercase leading-tight text-white! ${
              compact ? 'text-3xl sm:text-4xl' : 'text-3xl sm:text-5xl'
            }`}
          >
            {titleKey ? <AccentTitle i18nKey={titleKey} tone="dark" /> : title}
          </h1>
          {children}
        </div>
        {aside && <div className="animate-rise [animation-delay:150ms]">{aside}</div>}
      </div>
    </header>
  );
}

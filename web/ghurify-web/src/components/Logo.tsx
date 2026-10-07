import { useId } from 'react';
import { useTranslation } from 'react-i18next';

/*
 * The Ghurify mark: a G drawn as a journey. The solid stroke is the road travelled; over the top
 * it breaks into a dotted trail that leads to a rising sun, the destination; a soft line of hills
 * runs along the foot of the tile. The same drawing is public/favicon.svg, the app icons and the
 * email logo, so change them together.
 */
const G = 'M24.5 25.5H35A12 12 0 1 1 11.41 22.39';
const TRAIL = 'M13.67 17.95A12 12 0 0 1 25.49 13.76';
const HILLS = 'M0 38Q9 32 17 36Q26 29.5 35 34.5Q41.5 31.5 48 33V48H0Z';

/**
 * The mark alone, as a rounded tile. Green on light backgrounds; `inverted` makes it a white tile
 * for photos and dark bands.
 */
export function LogoMark({
  inverted = false,
  className = 'h-10 w-10',
}: {
  inverted?: boolean;
  className?: string;
}) {
  // Ids are unique per instance: the header, the menu and the footer can all be on screen.
  const id = useId();
  const tile = `${id}-tile`;
  const clip = `${id}-clip`;
  const ink = inverted ? '#245c43' : '#ffffff';

  return (
    <svg
      viewBox="0 0 48 48"
      className={`shrink-0 ${className}`}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={tile} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#2f7a57" />
          <stop offset="1" stopColor="#173f2e" />
        </linearGradient>
        <clipPath id={clip}>
          <rect width="48" height="48" rx="14" />
        </clipPath>
      </defs>
      <rect width="48" height="48" rx="14" fill={inverted ? '#ffffff' : `url(#${tile})`} />
      <path
        d={HILLS}
        clipPath={`url(#${clip})`}
        fill={inverted ? '#245c43' : '#0f2a1f'}
        opacity={inverted ? 0.1 : 0.4}
      />
      <circle cx="32.86" cy="15.29" r="7" fill="#d99a12" opacity="0.22" />
      <circle cx="32.86" cy="15.29" r="4.4" fill="#d99a12" />
      <path
        d={G}
        fill="none"
        stroke={ink}
        strokeWidth="5.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d={TRAIL}
        fill="none"
        stroke={ink}
        strokeWidth="2.7"
        strokeLinecap="round"
        strokeDasharray="0 4.2"
      />
    </svg>
  );
}

/**
 * The mark with the name beside it. In English the dot of the "i" is the same turmeric sun as
 * the mark; in Bangla the name is set as it is. `collapsible` drops the name on the narrowest
 * phones, where the header has no room for it; the mark carries the brand alone there.
 */
export function Logo({
  inverted = false,
  collapsible = false,
}: {
  inverted?: boolean;
  collapsible?: boolean;
}) {
  const { t } = useTranslation();
  const name = t('app.name');
  const dot = name.indexOf('i');

  return (
    <span className="flex items-center gap-2.5">
      <LogoMark
        inverted={inverted}
        className="h-9 w-9 drop-shadow-[0_4px_10px_rgba(15,42,31,0.18)] sm:h-10 sm:w-10"
      />
      <span
        className={`font-display text-[1.4rem] font-extrabold leading-none tracking-tight sm:text-[1.6rem] ${
          inverted ? 'text-white' : 'text-deep'
        } ${collapsible ? 'max-[23rem]:sr-only' : ''}`}
      >
        {dot < 0 ? (
          name
        ) : (
          <>
            <span className="sr-only">{name}</span>
            <span aria-hidden="true">
              {name.slice(0, dot)}
              <span className="relative">
                {'ı'}
                <span className="absolute left-1/2 top-[0.25em] h-[0.21em] w-[0.21em] -translate-x-1/2 rounded-full bg-turmeric" />
              </span>
              {name.slice(dot + 1)}
            </span>
          </>
        )}
      </span>
    </span>
  );
}

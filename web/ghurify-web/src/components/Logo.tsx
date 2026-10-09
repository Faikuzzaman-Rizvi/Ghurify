import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { assetUrl } from '@/features/site/siteApi';
import { useSiteConfig, useSiteName } from '@/features/site/useSiteConfig';

/*
 * The site's mark.
 *
 * By default it is the Ghurify drawing below: a G drawn as a journey. The solid stroke is the
 * road travelled; over the top it breaks into a dotted trail that leads to a rising sun, the
 * destination; a soft line of hills runs along the foot of the tile. The same drawing is
 * public/favicon.svg, the app icons and the email logo, so change them together.
 *
 * A super admin who uploads a logo replaces the drawing with their image, in the header and the
 * footer alike. Nothing else about the layout changes, so a logo of any sensible shape fits.
 */
const G = 'M24.5 25.5H35A12 12 0 1 1 11.41 22.39';
const TRAIL = 'M13.67 17.95A12 12 0 0 1 25.49 13.76';
const HILLS = 'M0 38Q9 32 17 36Q26 29.5 35 34.5Q41.5 31.5 48 33V48H0Z';

/**
 * The mark alone, as a rounded tile: the uploaded logo when there is one, otherwise the drawing.
 * Green on light backgrounds; `inverted` makes it a white tile for photos and dark bands.
 *
 * Every colour is a theme token rather than a hex value, so recolouring the site in the panel
 * recolours the mark with it.
 */
export function LogoMark({
  inverted = false,
  className = 'h-10 w-10',
}: {
  inverted?: boolean;
  className?: string;
}) {
  const { data: config } = useSiteConfig();
  const name = useSiteName();

  // Two uploads, so a logo can be legible on both the white header and the dark footer. A site
  // that only uploads one gets that one in both places.
  const uploaded =
    assetUrl(config, inverted ? 'logo-dark' : 'logo') ?? assetUrl(config, 'logo');
  const isCustom = config?.assets.some(
    (asset) => (asset.kind === 'logo' || asset.kind === 'logo-dark') && asset.isCustom,
  );

  if (isCustom && uploaded) {
    return (
      <img
        src={uploaded}
        alt={name}
        className={`shrink-0 object-contain ${className}`}
        // The mark is decorative beside the name, which is rendered as text next to it; the alt
        // text carries the name for the cases where it stands alone.
        loading="eager"
        decoding="async"
      />
    );
  }

  return <GhurifyMark inverted={inverted} className={className} />;
}

/** The drawing the app ships with. */
function GhurifyMark({ inverted, className }: { inverted: boolean; className: string }) {
  // Ids are unique per instance: the header, the menu and the footer can all be on screen.
  const id = useId();
  const tile = `${id}-tile`;
  const clip = `${id}-clip`;

  return (
    <svg
      viewBox="0 0 48 48"
      className={`shrink-0 ${className}`}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={tile} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" className="[stop-color:var(--color-hill)]" />
          <stop offset="1" className="[stop-color:var(--color-deep)]" />
        </linearGradient>
        <clipPath id={clip}>
          <rect width="48" height="48" rx="14" />
        </clipPath>
      </defs>
      <rect
        width="48"
        height="48"
        rx="14"
        className={inverted ? 'fill-white' : ''}
        fill={inverted ? undefined : `url(#${tile})`}
      />
      <path
        d={HILLS}
        clipPath={`url(#${clip})`}
        className={inverted ? 'fill-hill opacity-10' : 'fill-night opacity-40'}
      />
      <circle cx="32.86" cy="15.29" r="7" className="fill-turmeric opacity-20" />
      <circle cx="32.86" cy="15.29" r="4.4" className="fill-turmeric" />
      <path
        d={G}
        fill="none"
        className={inverted ? 'stroke-hill' : 'stroke-white'}
        strokeWidth="5.2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d={TRAIL}
        fill="none"
        className={inverted ? 'stroke-hill' : 'stroke-white'}
        strokeWidth="2.7"
        strokeLinecap="round"
        strokeDasharray="0 4.2"
      />
    </svg>
  );
}

/**
 * The mark with the name beside it. In English the dot of the "i" is the same turmeric sun as
 * the mark; in Bangla, or in any name without an "i", the name is set as it is. `collapsible`
 * drops the name on the narrowest phones, where the header has no room for it; the mark carries
 * the brand alone there.
 *
 * The name comes from the site's configuration, so renaming the site in the panel renames it
 * here. Until the configuration has loaded it is the name the app shipped with, which keeps the
 * header from being briefly empty.
 */
export function Logo({
  inverted = false,
  collapsible = false,
}: {
  inverted?: boolean;
  collapsible?: boolean;
}) {
  const { i18n } = useTranslation();
  const name = useSiteName();

  // The sun replaces the dot of a lower-case "i", which only makes sense in Latin script.
  const dot = i18n.language.startsWith('bn') ? -1 : name.indexOf('i');

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

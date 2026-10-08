import { useEffect, useId, useRef, useState, type ReactNode, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';

import { LogoMark } from '@/components/Logo';
import { latToY, lonToX } from '@/components/ui/mapProjection';
import { formatCount, toLanguage } from '@/lib/format';
import { countryOutline, districtShapes, divisionLines } from './districtShapes';
import {
  districtBySlug,
  districtCount,
  layoutLabels,
  mapBox,
  pinColours,
  pinPath,
} from './districts';
import type { MapTheme } from './mapThemes';

/** A place on the district map. */
export interface DistrictPin {
  key: string;
  latitude: number;
  longitude: number;
  /** trip: a Ghurify trip. added: a place the person added. upcoming: a trip still to come. */
  tone: 'trip' | 'added' | 'upcoming';
  /** What the pin's button is called. */
  name: string;
}

/** How many drawing units one CSS pixel is: pins and names keep the same size on any screen. */
function useUnitsPerPixel(element: RefObject<HTMLElement | null>): number {
  const [units, setUnits] = useState(1);
  useEffect(() => {
    const node = element.current;
    if (!node || typeof ResizeObserver === 'undefined') return;
    const measure = () => {
      const { width, height } = node.getBoundingClientRect();
      if (width > 0 && height > 0) {
        setUnits(Math.max(mapBox.width / width, mapBox.height / height));
      }
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(node);
    return () => observer.disconnect();
  }, [element]);
  return units;
}

/**
 * Bangladesh's 64 districts, the visited ones filled in, with the places as pins. Hover shows a
 * district's name; a click chooses it. The districts are for pointers only: the checklist beside
 * the map is the same choice for keyboards and screen readers. Pins are real buttons.
 */
export function DistrictMap({
  theme,
  visited,
  pins = [],
  selectedDistrict = null,
  selectedPin = null,
  showLabels = true,
  onSelectDistrict,
  onSelectPin,
  className = '',
}: {
  theme: MapTheme;
  /** Visited district slug -> how many places there. */
  visited: ReadonlyMap<string, number>;
  pins?: readonly DistrictPin[];
  selectedDistrict?: string | null;
  selectedPin?: string | null;
  showLabels?: boolean;
  onSelectDistrict?: (slug: string) => void;
  onSelectPin?: (key: string) => void;
  className?: string;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const id = useId().replace(/:/g, '');
  const box = useRef<HTMLDivElement>(null);
  const units = useUnitsPerPixel(box);
  const [hover, setHover] = useState<{ slug: string; x: number; y: number } | null>(null);

  const hovered = hover ? districtBySlug.get(hover.slug) : undefined;
  const outlined = [selectedDistrict, hover?.slug].filter(
    (slug, index, all): slug is string => !!slug && all.indexOf(slug) === index,
  );
  const labelSize = 10.5 * units;
  const pinSize = 8 * units;

  return (
    <div
      ref={box}
      className={`relative ${className}`}
      onMouseLeave={() => setHover(null)}
      style={{ aspectRatio: `${mapBox.width} / ${mapBox.height}` }}
    >
      <svg
        viewBox={`${mapBox.x} ${mapBox.y} ${mapBox.width} ${mapBox.height}`}
        className="absolute inset-0 h-full w-full overflow-visible"
        role="group"
        aria-label={t('travel.districts.mapLabel', {
          visited: formatCount(visited.size, language),
          total: formatCount(districtCount, language),
        })}
      >
        <defs>
          <linearGradient id={`${id}-visited`} x1="0" y1="0" x2="0.4" y2="1">
            <stop offset="0" stopColor={theme.visited[0]} />
            <stop offset="1" stopColor={theme.visited[1]} />
          </linearGradient>
          <filter id={`${id}-shadow`} x="-10%" y="-10%" width="120%" height="120%">
            <feGaussianBlur stdDeviation="5" />
          </filter>
        </defs>

        <path
          d={countryOutline}
          fill={theme.ink}
          opacity={theme.id === 'night' ? 0.5 : 0.12}
          transform="translate(3 6)"
          filter={`url(#${id}-shadow)`}
          aria-hidden="true"
        />

        <g aria-hidden="true" strokeLinejoin="round">
          {districtShapes.map((shape) => (
            <path
              key={shape.slug}
              d={shape.d}
              fillRule="evenodd"
              fill={theme.land}
              stroke={theme.border}
              strokeWidth={0.7}
              className={onSelectDistrict ? 'cursor-pointer' : undefined}
              onClick={onSelectDistrict ? () => onSelectDistrict(shape.slug) : undefined}
              onMouseMove={(event) => {
                const rect = box.current?.getBoundingClientRect();
                if (rect) {
                  setHover({
                    slug: shape.slug,
                    x: event.clientX - rect.left,
                    y: event.clientY - rect.top,
                  });
                }
              }}
            />
          ))}
          {/* Visited districts fade in over the land, so a new one arrives gently. */}
          {districtShapes
            .filter((shape) => visited.has(shape.slug))
            .map((shape) => (
              <path
                key={shape.slug}
                d={shape.d}
                fillRule="evenodd"
                fill={`url(#${id}-visited)`}
                stroke={theme.border}
                strokeWidth={0.7}
                pointerEvents="none"
                className="motion-safe:animate-fade-in"
              />
            ))}
          <path
            d={divisionLines}
            fill="none"
            stroke={theme.divisionLine}
            strokeWidth={1.3}
            strokeLinecap="round"
            pointerEvents="none"
          />
          {outlined.map((slug) => (
            <path
              key={slug}
              d={districtBySlug.get(slug)?.d}
              fillRule="evenodd"
              fill={slug === selectedDistrict ? theme.highlight : 'none'}
              fillOpacity={0.28}
              stroke={theme.highlight}
              strokeWidth={2.2 * Math.min(units, 1.6)}
              pointerEvents="none"
            />
          ))}
        </g>

        {/* The chosen pin last, so it is drawn on top. */}
        {[...pins]
          .sort((a, b) => Number(a.key === selectedPin) - Number(b.key === selectedPin))
          .map((pin) => {
            const active = pin.key === selectedPin;
            const size = pinSize * (active ? 1.35 : 1);
            const select = onSelectPin ? () => onSelectPin(pin.key) : undefined;
            return (
              <g
                key={pin.key}
                transform={`translate(${lonToX(pin.longitude)} ${latToY(pin.latitude)})`}
                role={select ? 'button' : undefined}
                tabIndex={select ? 0 : undefined}
                aria-label={select ? pin.name : undefined}
                aria-pressed={select ? active : undefined}
                aria-hidden={select ? undefined : true}
                className={
                  select
                    ? 'cursor-pointer outline-none [&:focus-visible>circle]:opacity-100'
                    : undefined
                }
                onClick={select}
                onKeyDown={(event) => {
                  if (select && (event.key === 'Enter' || event.key === ' ')) {
                    event.preventDefault();
                    select();
                  }
                }}
              >
                {/* A larger, invisible target for fingers; and the focus ring. */}
                <circle
                  cy={-size * 1.6}
                  r={size * 1.9}
                  fill={theme.highlight}
                  opacity={active ? 0.4 : 0}
                />
                <path
                  d={pinPath}
                  transform={`scale(${size})`}
                  fill={pin.tone === 'upcoming' ? '#ffffff' : pinColours[pin.tone]}
                  stroke={pin.tone === 'upcoming' ? pinColours.upcoming : '#ffffff'}
                  strokeWidth={((pin.tone === 'upcoming' ? 2.4 : 2) / size) * units}
                  strokeDasharray={
                    pin.tone === 'upcoming'
                      ? `${(2.6 / size) * units} ${(1.8 / size) * units}`
                      : undefined
                  }
                />
                <circle
                  cy={-size * 1.8}
                  r={size * 0.38}
                  fill={pin.tone === 'upcoming' ? pinColours.upcoming : '#ffffff'}
                />
              </g>
            );
          })}
        {/* Names over the pins: a pin stays findable around a name, a name under a pin does not. */}
        {showLabels && (
          <g aria-hidden="true" pointerEvents="none" textAnchor="middle">
            {layoutLabels(
              districtShapes
                .filter((shape) => visited.has(shape.slug))
                .map((shape) => ({ slug: shape.slug, text: t(`district.${shape.slug}`) })),
              labelSize,
            ).map((label) => (
              <text
                key={label.slug}
                x={label.x}
                y={label.y}
                dy="0.35em"
                fontSize={labelSize}
                fontWeight={700}
                fill={theme.label}
                stroke={theme.labelHalo}
                strokeWidth={labelSize * 0.32}
                strokeLinejoin="round"
                paintOrder="stroke"
                className="font-display"
              >
                {label.text}
              </text>
            ))}
          </g>
        )}
      </svg>

      {hover && hovered && (
        <div
          aria-hidden="true"
          className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-[calc(100%+12px)] whitespace-nowrap rounded-xl bg-night/90 px-3 py-1.5 text-xs font-semibold text-white shadow-lg backdrop-blur"
          style={{ left: hover.x, top: hover.y }}
        >
          {t(`district.${hovered.slug}`)}
          <span className="ml-1.5 font-normal text-white/70">
            {visited.has(hovered.slug)
              ? t('travel.districts.places', {
                  count: visited.get(hovered.slug) ?? 0,
                  n: formatCount(visited.get(hovered.slug) ?? 0, language),
                })
              : t('travel.districts.notYet')}
          </span>
        </div>
      )}
    </div>
  );
}

/**
 * The map as a poster, the way it downloads: whose Bangladesh, how many of the 64 districts, the
 * map, how far along they are, and Ghurify's name at the foot.
 */
export function TravelPoster({
  theme,
  name,
  visitedDistricts,
  divisionsReached,
  photo,
  children,
}: {
  theme: MapTheme;
  name: string;
  visitedDistricts: number;
  divisionsReached: number;
  /** Their picture beside the title, when they have one. */
  photo?: ReactNode;
  /** The map. */
  children: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const percent = Math.round((visitedDistricts / districtCount) * 100);

  return (
    <figure
      className="relative overflow-hidden rounded-[1.75rem] px-5 pb-5 pt-6 transition-colors duration-500 sm:px-8 sm:pt-8"
      style={{ background: theme.paper, color: theme.ink }}
    >
      <header className="flex items-start justify-between gap-4">
        <div className="flex min-w-0 items-center gap-3">
          {photo}
          <div className="min-w-0">
            <p
              className="text-[0.7rem] font-semibold uppercase tracking-[0.2em]"
              style={{ color: theme.muted }}
            >
              {t('travel.poster.eyebrow')}
            </p>
            <h3
              className="mt-1 font-display text-2xl font-extrabold leading-tight sm:text-3xl"
              style={{ color: theme.ink }}
            >
              {name ? t('travel.poster.title', { name }) : t('travel.poster.titleMine')}
            </h3>
          </div>
        </div>
        <p className="shrink-0 text-right font-display leading-none" aria-hidden="true">
          <span className="text-5xl font-extrabold sm:text-6xl" style={{ color: theme.accent }}>
            {formatCount(visitedDistricts, language)}
          </span>
          <span className="text-xl font-bold" style={{ color: theme.muted }}>
            /{formatCount(districtCount, language)}
          </span>
        </p>
      </header>

      <div className="mx-auto mt-2 w-full max-w-[34rem]">{children}</div>

      <figcaption className="mt-4">
        <div
          className="h-2 overflow-hidden rounded-full"
          style={{ background: theme.track }}
          aria-hidden="true"
        >
          <div
            className="h-full rounded-full transition-[width] duration-700"
            style={{
              width: `${Math.max(percent, visitedDistricts > 0 ? 2 : 0)}%`,
              background: `linear-gradient(90deg, ${theme.visited[0]}, ${theme.accent})`,
            }}
          />
        </div>
        <div className="mt-2.5 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 text-sm">
          <p className="font-bold">
            {t('travel.poster.explored', { percent: formatCount(percent, language) })}
          </p>
          <p style={{ color: theme.muted }}>
            {t('travel.poster.counts', {
              count: visitedDistricts,
              districts: formatCount(visitedDistricts, language),
              divisions: formatCount(divisionsReached, language),
              totalDivisions: formatCount(8, language),
            })}
          </p>
        </div>
        <div
          className="mt-4 flex items-center justify-center gap-2 border-t pt-3 text-xs"
          style={{ borderColor: theme.track, color: theme.muted }}
        >
          <LogoMark className="h-5 w-5" />
          <span className="font-display font-bold" style={{ color: theme.ink }}>
            {t('app.name')}
          </span>
          <span aria-hidden="true">·</span>
          <span>{t('travel.poster.tagline')}</span>
        </div>
      </figcaption>
    </figure>
  );
}

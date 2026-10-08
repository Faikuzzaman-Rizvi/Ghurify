import { useEffect, useMemo } from 'react';
import L from 'leaflet';
import { MapContainer, Marker, TileLayer, Tooltip, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { useTranslation } from 'react-i18next';
import { toLanguage } from '@/lib/format';
import { pointOf, upcomingKey, type UpcomingTrip, type VisitedPlace } from './travelApi';

/** Bangladesh, and a margin of sea and border the map may be dragged into but no further. */
const bangladesh = L.latLngBounds([20.55, 88.0], [26.65, 92.7]);
const roam = L.latLngBounds([19.4, 86.4], [27.7, 94.2]);

type Tone = 'trip' | 'added' | 'upcoming';

/** One pin as the map draws it. */
interface Pin {
  key: string;
  point: [number, number];
  tone: Tone;
  count: number;
  label: string;
}

const toneClass: Record<Tone, string> = {
  trip: 'bg-hill text-white',
  added: 'bg-turmeric text-deep',
  upcoming: 'border-2 border-dashed border-jamdani bg-white text-jamdani',
};

/**
 * A teardrop pin drawn in HTML: a square with three round corners, turned 45° so the square one
 * points down at the spot. A count when the place was visited more than once, else a dot.
 */
function pinIcon(tone: Tone, count: number, selected: boolean): L.DivIcon {
  const inner =
    count > 1
      ? `<span class="rotate-45 text-xs font-bold">${count}</span>`
      : '<span class="rotate-45 block h-2.5 w-2.5 rounded-full bg-current opacity-90"></span>';

  return L.divIcon({
    // Replaces Leaflet's default white box around a div icon.
    className: 'ghurify-pin',
    html: `<span class="flex h-9 w-9 -rotate-45 items-center justify-center rounded-full rounded-bl-none shadow-[0_6px_14px_rgba(15,42,31,0.35)] transition-transform duration-200 ${toneClass[tone]} ${
      selected ? 'scale-125 ring-4 ring-dusk' : 'ring-2 ring-white'
    }">${inner}</span>`,
    iconSize: [36, 36],
    // The point of the turned square sits 25px below its centre.
    iconAnchor: [18, 43],
    tooltipAnchor: [0, -40],
  });
}

const reducedMotion = () =>
  typeof window !== 'undefined' &&
  Boolean(window.matchMedia?.('(prefers-reduced-motion: reduce)').matches);

/** Frames every pin when the set of pins changes; the whole country when there are none. */
function FitToPins({ points, signature }: { points: [number, number][]; signature: string }) {
  const map = useMap();

  useEffect(() => {
    if (points.length === 0) {
      map.fitBounds(bangladesh);
    } else {
      map.fitBounds(L.latLngBounds(points).pad(0.3), { maxZoom: 9 });
    }
    // Only when the pins themselves change, not on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, signature]);

  return null;
}

/** Brings the chosen pin into view. */
function FlyToSelected({ point }: { point: [number, number] | null }) {
  const map = useMap();
  const lat = point?.[0];
  const lng = point?.[1];

  useEffect(() => {
    if (lat === undefined || lng === undefined) return;
    const zoom = Math.max(map.getZoom(), 9);
    if (reducedMotion()) map.setView([lat, lng], zoom);
    else map.flyTo([lat, lng], zoom, { duration: 0.8 });
  }, [map, lat, lng]);

  return null;
}

export interface TravelMapCanvasProps {
  places: readonly VisitedPlace[];
  upcoming?: readonly UpcomingTrip[];
  selectedKey: string | null;
  onSelect: (key: string) => void;
  /** Off where the map sits inside a scrolling page, so scrolling never gets caught by it. */
  scrollWheelZoom?: boolean;
  className?: string;
}

/**
 * The travel map: Bangladesh, a pin for every place someone has been (deep green for Ghurify
 * trips, turmeric for places they added) and dashed pins for trips to come. Clicking a pin, or
 * focusing it and pressing Enter, selects it. Loaded on demand: Leaflet is most of its weight.
 */
export default function TravelMapCanvas({
  places,
  upcoming = [],
  selectedKey,
  onSelect,
  scrollWheelZoom = true,
  className = '',
}: TravelMapCanvasProps) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);

  const pins = useMemo(() => {
    const visitedSlugs = new Set(places.map((place) => place.destinationSlug).filter(Boolean));

    const visited = places.flatMap((place): Pin[] => {
      const point = pointOf(place);
      if (!point) return [];
      const fromTrips = place.visits.some((visit) => visit.source === 'Trip');
      return [
        {
          key: place.key,
          point,
          tone: fromTrips ? 'trip' : 'added',
          count: place.visits.length,
          label: language === 'bn' && place.nameBn ? place.nameBn : place.name,
        },
      ];
    });

    const coming = upcoming.flatMap((trip): Pin[] => {
      const point = pointOf(trip);
      if (!point) return [];
      // Nudged north of a pin already there, so both stay visible and clickable.
      const nudged: [number, number] = visitedSlugs.has(trip.destinationSlug)
        ? [point[0] + 0.05, point[1]]
        : point;
      return [
        {
          key: upcomingKey(trip),
          point: nudged,
          tone: 'upcoming',
          count: 1,
          label: t('travel.map.comingUpPin', {
            place: language === 'bn' ? trip.destinationNameBn : trip.destinationName,
          }),
        },
      ];
    });

    return [...visited, ...coming];
  }, [places, upcoming, language, t]);

  const signature = pins.map((pin) => pin.key).join('|');
  const selected = pins.find((pin) => pin.key === selectedKey) ?? null;

  return (
    <MapContainer
      center={[23.7, 90.35]}
      zoom={7}
      minZoom={6}
      maxBounds={roam}
      maxBoundsViscosity={0.9}
      scrollWheelZoom={scrollWheelZoom}
      className={`h-full w-full ${className}`}
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <FitToPins points={pins.map((pin) => pin.point)} signature={signature} />
      <FlyToSelected point={selected?.point ?? null} />
      {pins.map((pin) => (
        <Marker
          key={pin.key}
          position={pin.point}
          icon={pinIcon(pin.tone, pin.count, pin.key === selectedKey)}
          title={pin.label}
          zIndexOffset={pin.key === selectedKey ? 1000 : 0}
          eventHandlers={{ click: () => onSelect(pin.key) }}
        >
          <Tooltip direction="top">{pin.label}</Tooltip>
        </Marker>
      ))}
    </MapContainer>
  );
}

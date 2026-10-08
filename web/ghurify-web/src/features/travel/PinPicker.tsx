import { useEffect, useState } from 'react';
import L from 'leaflet';
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';

const bangladesh = L.latLngBounds([20.55, 88.0], [26.65, 92.7]);

const pin = L.divIcon({
  className: 'ghurify-pin',
  html: '<span class="flex h-8 w-8 -rotate-45 items-center justify-center rounded-full rounded-bl-none bg-turmeric shadow-lg ring-2 ring-white"><span class="rotate-45 block h-2.5 w-2.5 rounded-full bg-deep"></span></span>',
  iconSize: [32, 32],
  iconAnchor: [16, 38],
});

/** A click on the map moves the pin there. */
function ClickToPlace({ onPick }: { onPick: (point: [number, number]) => void }) {
  useMapEvents({ click: (event) => onPick([event.latlng.lat, event.latlng.lng]) });
  return null;
}

/**
 * Opened inside a dialog the map has no size at first, and framing it then gets the zoom and the
 * centre wrong. So it waits until it has a size, measures again whenever that changes, and frames
 * itself once: on the pin it started with (a place in a district), else on all of Bangladesh.
 */
function Frame({ start }: { start: [number, number] | null }) {
  const map = useMap();
  useEffect(() => {
    const container = map.getContainer();
    let framed = false;
    const frame = () => {
      if (container.clientWidth === 0 || container.clientHeight === 0) return;
      map.invalidateSize({ animate: false });
      if (framed) return;
      framed = true;
      if (start) map.setView(start, 8, { animate: false });
      else map.fitBounds(bangladesh, { animate: false });
    };

    frame();
    if (typeof ResizeObserver === 'undefined') {
      const timer = window.setTimeout(frame, 60);
      return () => window.clearTimeout(timer);
    }
    const observer = new ResizeObserver(frame);
    observer.observe(container);
    return () => observer.disconnect();
  }, [map, start]);
  return null;
}

/**
 * A small map of Bangladesh to put a pin on: for a place that is not one of Ghurify's
 * destinations. Loaded on demand with the travel map.
 */
export default function PinPicker({
  point,
  onPick,
  label,
}: {
  point: [number, number] | null;
  onPick: (point: [number, number]) => void;
  label: string;
}) {
  // Where it starts; later taps move the pin without moving the map.
  const [start] = useState(point);

  return (
    <div
      className="h-56 overflow-hidden rounded-xl ring-1 ring-hill/15 sm:h-64"
      role="application"
      aria-label={label}
    >
      <MapContainer
        bounds={bangladesh}
        minZoom={6}
        maxBounds={bangladesh.pad(0.15)}
        maxBoundsViscosity={0.9}
        className="h-full w-full cursor-crosshair"
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Frame start={start} />
        <ClickToPlace onPick={onPick} />
        {point && <Marker position={point} icon={pin} />}
      </MapContainer>
    </div>
  );
}

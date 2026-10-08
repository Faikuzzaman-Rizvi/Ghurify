/**
 * The one projection every drawn map of Bangladesh uses (the artwork, the travel map's districts
 * and its pins): equirectangular at 23.7°N, so x is longitude squeezed by cos 23.7°, y is latitude.
 * Units are the drawings' viewBox units; about one per kilometre.
 */
export const lonToX = (lon: number) => (lon - 88) * 91.57 + 12;
export const latToY = (lat: number) => (26.65 - lat) * 100 + 11.33;

/** Back again: where a point on the drawing is on the ground, as [latitude, longitude]. */
export const toLatLng = (x: number, y: number): [number, number] => [
  26.65 - (y - 11.33) / 100,
  (x - 12) / 91.57 + 88,
];

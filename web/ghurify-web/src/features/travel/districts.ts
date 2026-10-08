import { latToY, lonToX } from '@/components/ui/mapProjection';
import { districtFrame, districtShapes, type DistrictShape } from './districtShapes';
import { divisions, pointOf, type Division, type VisitedPlace } from './travelApi';

export type { DistrictShape };

/** Room around the country, so a pin on the coast or a label at the edge is not cut off. */
const pad = 10;
export const mapBox = {
  x: districtFrame.x - pad,
  y: districtFrame.y - pad - 8,
  width: districtFrame.width + pad * 2,
  height: districtFrame.height + pad * 2 + 8,
} as const;

export const pinColours = { trip: '#245c43', added: '#d99a12', upcoming: '#a3305c' } as const;

/** The teardrop pin, its tip at (0, 0), for a radius of 1. */
export const pinPath = 'M0 0C-.35-.55-1-1.15-1-1.8A1 1 0 1 1 1-1.8C1-1.15.35-.55 0 0Z';

/** Bangladesh's 64 districts. */
export const districtCount = districtShapes.length;

/** Each division's districts, in the order the checklist shows them. */
export const districtsByDivision: ReadonlyArray<{
  division: Division;
  districts: readonly DistrictShape[];
}> = divisions.map((division) => ({
  division,
  districts: districtShapes.filter((shape) => shape.division === division),
}));

export const districtBySlug = new Map(districtShapes.map((shape) => [shape.slug, shape]));

type Ring = ReadonlyArray<readonly [number, number]>;

/** The outlines as point lists, parsed once from the path data ("Mx,y x,y …Z" per ring). */
let rings: Map<string, Ring[]> | null = null;
function ringsOf(shape: DistrictShape): Ring[] {
  rings ??= new Map(
    districtShapes.map((item) => [
      item.slug,
      item.d
        .split('M')
        .filter(Boolean)
        .map((ring) =>
          ring
            .replace('Z', '')
            .trim()
            .split(' ')
            .map((pair) => {
              const [x, y] = pair.split(',');
              return [Number(x), Number(y)] as const;
            }),
        ),
    ]),
  );
  return rings.get(shape.slug) ?? [];
}

/** Even-odd: inside when a ray from the point crosses the outline an odd number of times. */
function contains(shape: DistrictShape, x: number, y: number): boolean {
  let inside = false;
  for (const ring of ringsOf(shape)) {
    for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
      const [xi, yi] = ring[i]!;
      const [xj, yj] = ring[j]!;
      if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
    }
  }
  return inside;
}

function distanceTo(shape: DistrictShape, x: number, y: number): number {
  let best = Infinity;
  for (const ring of ringsOf(shape)) {
    for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
      const [ax, ay] = ring[j]!;
      const [bx, by] = ring[i]!;
      const length = (bx - ax) ** 2 + (by - ay) ** 2;
      const t =
        length === 0
          ? 0
          : Math.max(0, Math.min(1, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / length));
      best = Math.min(best, Math.hypot(x - (ax + t * (bx - ax)), y - (ay + t * (by - ay))));
    }
  }
  return best;
}

/**
 * Bangladesh's coast is drawn simplified, so a beach or a char can sit just off its district's
 * outline: within this reach (about 6 km) of the coast, the nearest district counts. Only along
 * the coast, south of 23°N: inland, a point just outside the outline is across a border (Agartala
 * is 3 km from Brahmanbaria).
 */
const coastReach = 6;
const coastNorth = 23;

/** The district a point on the ground falls in, or null when it is outside Bangladesh. */
export function districtAt(latitude: number, longitude: number): DistrictShape | null {
  const x = lonToX(longitude);
  const y = latToY(latitude);
  const inside = districtShapes.find((shape) => contains(shape, x, y));
  if (inside || latitude >= coastNorth) return inside ?? null;

  let nearest: DistrictShape | null = null;
  let best = coastReach;
  for (const shape of districtShapes) {
    const distance = distanceTo(shape, x, y);
    if (distance <= best) {
      best = distance;
      nearest = shape;
    }
  }
  return nearest;
}

/** Each visited district's places, keyed by district slug. Places without a point are left out. */
export function placesByDistrict(places: readonly VisitedPlace[]): Map<string, VisitedPlace[]> {
  const found = new Map<string, VisitedPlace[]>();
  for (const place of places) {
    const point = pointOf(place);
    const district = point && districtAt(point[0], point[1]);
    if (district) found.set(district.slug, [...(found.get(district.slug) ?? []), place]);
  }
  return found;
}

/** A district's bounding box on the drawing: where its fill gradient runs from and to. */
export function districtBounds(slug: string): {
  x: number;
  y: number;
  width: number;
  height: number;
} {
  const shape = districtBySlug.get(slug);
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const ring of shape ? ringsOf(shape) : []) {
    for (const [x, y] of ring) {
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }
  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

/** A district name's place on the map: its own spot, or nudged off a neighbour's. */
export interface PlacedLabel {
  slug: string;
  text: string;
  x: number;
  y: number;
}

/**
 * Places the visited districts' names so none overlap: each takes its district's spot when it
 * is free, else the first free one just below, above, or beside it. `size` is the font size in
 * drawing units; widths are estimated, which is close enough for short names.
 */
export function layoutLabels(
  items: ReadonlyArray<{ slug: string; text: string }>,
  size: number,
): PlacedLabel[] {
  const placed: Array<PlacedLabel & { width: number }> = [];
  const height = size * 1.25;
  const overlaps = (x: number, y: number, width: number) =>
    placed.some(
      (other) =>
        Math.abs(other.x - x) < (other.width + width) / 2 + size * 0.5 &&
        Math.abs(other.y - y) < height,
    );

  const ordered = items
    .map((item) => ({ ...item, at: districtBySlug.get(item.slug)?.label }))
    .filter((item): item is typeof item & { at: readonly [number, number] } => !!item.at)
    .sort((a, b) => a.at[1] - b.at[1] || a.at[0] - b.at[0]);

  for (const item of ordered) {
    const width = item.text.length * size * 0.64;
    const [x, y] = item.at;
    const tries: Array<[number, number]> = [
      [0, 0],
      [0, height],
      [0, -height],
      [width * 0.5, height * 0.5],
      [-width * 0.5, -height * 0.5],
      [0, height * 2],
      [0, -height * 2],
    ];
    const [dx, dy] = tries.find(([tx, ty]) => !overlaps(x + tx, y + ty, width)) ?? [0, 0];
    placed.push({ slug: item.slug, text: item.text, x: x + dx, y: y + dy, width });
  }
  return placed.map(({ slug, text, x, y }) => ({ slug, text, x, y }));
}

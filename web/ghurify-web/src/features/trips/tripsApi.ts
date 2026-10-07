import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type TripPage = components['schemas']['TripPage'];
export type TripSummary = components['schemas']['TripSummary'];
export type TripDetail = components['schemas']['TripDetail'];
export type DestinationSummary = components['schemas']['DestinationSummary'];
export type DestinationKind = components['schemas']['DestinationKind'];
export type DestinationStatus = components['schemas']['DestinationStatus'];
export type GroupType = components['schemas']['GroupType'];
export type CostCategory = components['schemas']['CostCategory'];
export type Difficulty = components['schemas']['Difficulty'];
export type TripSort = NonNullable<components['schemas']['TripSort']>;

export const groupTypes: readonly GroupType[] = ['Open', 'WomenOnly', 'Students', 'Families'];
export const tripSorts: readonly TripSort[] = ['Soonest', 'PriceLowToHigh', 'PriceHighToLow'];

/** The explore page's filters. Mirrors the query string, which is their source of truth. */
export interface TripFilters {
  destination?: string | undefined;
  from?: string | undefined;
  groupType?: GroupType | undefined;
  maxPrice?: number | undefined;
  sort?: TripSort | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
  verifiedHostsOnly?: boolean | undefined;
}

function toQueryString(filters: TripFilters): string {
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== '') {
      params.set(key, String(value));
    }
  }

  const query = params.toString();
  return query ? `?${query}` : '';
}

/**
 * Trip discovery. Anonymous endpoints, but the bearer token is still sent when there is one:
 * who is asking decides whether women-only trips are shown.
 */
export const tripsApi = {
  search: (filters: TripFilters, signal?: AbortSignal) =>
    apiGet<TripPage>(`/api/v1/trips${toQueryString(filters)}`, signal ? { signal } : {}),

  get: (id: number, signal?: AbortSignal) =>
    apiGet<TripDetail>(`/api/v1/trips/${id}`, signal ? { signal } : {}),

  destinations: (signal?: AbortSignal) =>
    apiGet<DestinationSummary[]>('/api/v1/destinations', signal ? { signal } : {}),

  destination: (slug: string, signal?: AbortSignal) =>
    apiGet<DestinationSummary>(
      `/api/v1/destinations/${encodeURIComponent(slug)}`,
      signal ? { signal } : {},
    ),
};

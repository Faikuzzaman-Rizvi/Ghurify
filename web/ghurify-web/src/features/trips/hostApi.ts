import { apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';
import type { TripDetail } from './tripsApi';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type SaveTripCommand = components['schemas']['SaveTripCommand'];
export type HostTripSummary = components['schemas']['HostTripSummary'];
export type TripCreated = components['schemas']['TripCreated'];
export type TripStatus = components['schemas']['TripStatus'];

/** A host's own trips. The API filters every call by the signed-in host. */
export const hostApi = {
  myTrips: (signal?: AbortSignal) =>
    apiGet<HostTripSummary[]>('/api/v1/me/trips', signal ? { signal } : {}),

  create: (command: SaveTripCommand) => apiPost<TripCreated>('/api/v1/trips', command),

  update: (id: number, command: SaveTripCommand) =>
    apiPut<TripDetail>(`/api/v1/trips/${id}`, command),

  publish: (id: number) => apiPost<TripDetail>(`/api/v1/trips/${id}/publish`),

  cancel: (id: number) => apiPost<void>(`/api/v1/trips/${id}/cancel`),
};

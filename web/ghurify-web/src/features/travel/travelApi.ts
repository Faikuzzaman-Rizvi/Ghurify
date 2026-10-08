import { apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type TravelMap = components['schemas']['TravelMap'];
export type TravelSummary = components['schemas']['TravelSummary'];
export type VisitedPlace = components['schemas']['VisitedPlace'];
export type PlaceVisit = components['schemas']['PlaceVisit'];
export type PlacePhoto = components['schemas']['PlacePhoto'];
export type UpcomingTrip = components['schemas']['UpcomingTrip'];
export type VisitSource = components['schemas']['VisitSource'];
export type Division = NonNullable<components['schemas']['Division']>;
export type AddVisitCommand = components['schemas']['AddVisitCommand'];
export type EditVisitCommand = components['schemas']['EditVisitCommand'];

/** Photos on one visit: uploaded first (as story photos are), then put on it by id. */
export const maxPhotosPerVisit = 12;

/** Bangladesh's eight divisions, in the order the map's division strip shows them. */
export const divisions: readonly Division[] = [
  'Rangpur',
  'Rajshahi',
  'Mymensingh',
  'Sylhet',
  'Dhaka',
  'Khulna',
  'Barishal',
  'Chattogram',
];

const withSignal = (signal?: AbortSignal) => (signal ? { signal } : {});

export const travelApi = {
  mine: (signal?: AbortSignal) => apiGet<TravelMap>('/api/v1/me/travel-map', withSignal(signal)),

  /** Someone else's map: empty (shared = false) unless they chose to share it. */
  shared: (userId: number, signal?: AbortSignal) =>
    apiGet<TravelMap>(`/api/v1/users/${userId}/travel-map`, withSignal(signal)),

  addVisit: (command: AddVisitCommand) =>
    apiPost<{ id: number | string }>('/api/v1/me/travel-map/visits', command),

  editVisit: (visitId: number, command: EditVisitCommand) =>
    apiPut<void>(`/api/v1/me/travel-map/visits/${visitId}`, command),

  removeVisit: (visitId: number) => apiDelete<void>(`/api/v1/me/travel-map/visits/${visitId}`),

  setSharing: (share: boolean) => apiPut<void>('/api/v1/me/travel-map/sharing', { share }),

  addPhotos: (visitId: number, mediaIds: number[]) =>
    apiPost<void>(`/api/v1/me/travel-map/visits/${visitId}/photos`, { mediaIds }),

  removePhoto: (visitId: number, mediaId: number) =>
    apiDelete<void>(`/api/v1/me/travel-map/visits/${visitId}/photos/${mediaId}`),
};

/** A pin's position, when the place has one. */
export function pointOf(place: {
  latitude: number | string | null;
  longitude: number | string | null;
}): [number, number] | null {
  return place.latitude === null || place.longitude === null
    ? null
    : [Number(place.latitude), Number(place.longitude)];
}

/** Trip pins carry a "u:" key so they never clash with a place's. */
export const upcomingKey = (trip: UpcomingTrip) => `u:${String(trip.tripId)}`;

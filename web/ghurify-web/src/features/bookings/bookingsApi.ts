import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type MyTripBooking = components['schemas']['MyTripBooking'];
export type JoinRequestForHost = components['schemas']['JoinRequestForHost'];
export type JoinRequestCreated = components['schemas']['JoinRequestCreated'];
export type JoinRequestApproved = components['schemas']['JoinRequestApproved'];
export type JoinRequestStatus = NonNullable<components['schemas']['JoinRequestStatus']>;
export type BookingStatus = NonNullable<components['schemas']['BookingStatus']>;

/** Joining trips. Every call is checked for ownership by the API. */
export const bookingsApi = {
  requestToJoin: (tripId: number, message: string) =>
    apiPost<JoinRequestCreated>(`/api/v1/trips/${tripId}/join-requests`, {
      message: message.trim() || null,
    }),

  cancel: (requestId: number) => apiPost<void>(`/api/v1/join-requests/${requestId}/cancel`),

  mine: (signal?: AbortSignal) =>
    apiGet<MyTripBooking[]>('/api/v1/me/bookings', signal ? { signal } : {}),

  forTrip: (tripId: number, signal?: AbortSignal) =>
    apiGet<JoinRequestForHost[]>(`/api/v1/trips/${tripId}/join-requests`, signal ? { signal } : {}),

  approve: (requestId: number) =>
    apiPost<JoinRequestApproved>(`/api/v1/join-requests/${requestId}/approve`),

  decline: (requestId: number) => apiPost<void>(`/api/v1/join-requests/${requestId}/decline`),
};

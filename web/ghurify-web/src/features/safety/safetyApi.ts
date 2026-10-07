import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type SosRaisedView = components['schemas']['SosRaisedView'];
export type HelpPoint = components['schemas']['HelpPoint'];
export type CheckInView = components['schemas']['CheckInView'];
export type FileReportCommand = components['schemas']['FileReportCommand'];
export type ReportKind = components['schemas']['ReportKind'];
export type ReportReason = components['schemas']['ReportReason'];

export interface Position {
  latitude: number;
  longitude: number;
  accuracyMeters: number | null;
}

/** SOS, check-ins and reports, from the side of the people on a trip. */
export const safetyApi = {
  raiseSos: (tripId: number, position: Position, message: string | null) =>
    apiPost<SosRaisedView>(`/api/v1/trips/${tripId}/sos`, { ...position, message }),

  updateSosLocation: (sosId: number, position: Position) =>
    apiPost<void>(`/api/v1/sos/${sosId}/location`, {
      latitude: position.latitude,
      longitude: position.longitude,
    }),

  imSafe: (sosId: number) => apiPost<void>(`/api/v1/sos/${sosId}/resolve`),

  checkIns: (tripId: number, signal?: AbortSignal) =>
    apiGet<CheckInView[]>(`/api/v1/trips/${tripId}/check-ins`, signal ? { signal } : {}),

  scheduleCheckIn: (tripId: number, label: string, dueAt: string) =>
    apiPost<{ id: number | string }>(`/api/v1/trips/${tripId}/check-ins`, { label, dueAt }),

  completeCheckIn: (checkInId: number, note: string | null) =>
    apiPost<void>(`/api/v1/check-ins/${checkInId}/done`, { note }),

  fileReport: (command: FileReportCommand) =>
    apiPost<{ id: number | string }>('/api/v1/reports', command),
};

/**
 * The phone's position, once. Rejects when the browser has no geolocation or the person says no;
 * the SOS screen then asks them to describe where they are instead.
 */
export function currentPosition(): Promise<Position> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('no_geolocation'));
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (found) =>
        resolve({
          latitude: Number(found.coords.latitude.toFixed(6)),
          longitude: Number(found.coords.longitude.toFixed(6)),
          accuracyMeters: Number.isFinite(found.coords.accuracy)
            ? Math.round(found.coords.accuracy)
            : null,
        }),
      reject,
      { enableHighAccuracy: true, timeout: 15_000, maximumAge: 30_000 },
    );
  });
}

/** A map link for a position, readable on any phone without an app. */
export function mapLink(latitude: number | string, longitude: number | string): string {
  return `https://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=16/${latitude}/${longitude}`;
}

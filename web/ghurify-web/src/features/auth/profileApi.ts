import { apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type Profile = components['schemas']['ProfileDetails'];
export type UpdateProfileCommand = components['schemas']['UpdateProfileCommand'];
export type VerificationRecord = components['schemas']['VerificationRecord'];
export type VerificationLevel = components['schemas']['VerificationLevel'];
export type VerificationStatus = components['schemas']['VerificationStatus'];
export type StartVerificationRequest = components['schemas']['StartVerificationRequest'];
export type IdDocumentType = NonNullable<components['schemas']['IdDocumentType']>;
export type VerificationDocumentKind = components['schemas']['VerificationDocumentKind'];
export type MyDocumentView = components['schemas']['MyDocumentView'];
export type UploadTicket = components['schemas']['UploadTicket'];
export type Gender = components['schemas']['Gender'];
export type Role = components['schemas']['Role'];

export const genders: readonly Gender[] = ['Female', 'Male', 'Other'];

const apiBase = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * Someone's profile picture. The API redirects to a short-lived link, or answers 404 when there
 * is none (the Avatar component then shows initials). `version` busts the browser cache after a
 * change.
 */
export function avatarUrl(userId: number | string, version?: number | string | null): string {
  return `${apiBase}/api/v1/users/${userId}/avatar${version ? `?v=${version}` : ''}`;
}

/** The signed-in user's own profile, identity checks, ID photos and profile picture. */
export const profileApi = {
  get: (signal?: AbortSignal) => apiGet<Profile>('/api/v1/me/profile', signal ? { signal } : {}),

  update: (command: UpdateProfileCommand) => apiPut<Profile>('/api/v1/me/profile', command),

  becomeHost: () => apiPost<void>('/api/v1/me/roles/host'),

  verifications: (signal?: AbortSignal) =>
    apiGet<VerificationRecord[]>('/api/v1/me/verification', signal ? { signal } : {}),

  startVerification: (request: StartVerificationRequest) =>
    apiPost<VerificationRecord>('/api/v1/me/verification', request),

  documents: (signal?: AbortSignal) =>
    apiGet<MyDocumentView[]>('/api/v1/me/verification/documents', signal ? { signal } : {}),

  startDocumentUpload: (kind: VerificationDocumentKind, contentType: string, sizeBytes: number) =>
    apiPost<UploadTicket>('/api/v1/me/verification/documents', { kind, contentType, sizeBytes }),

  completeDocumentUpload: (id: number) =>
    apiPost<MyDocumentView>(`/api/v1/me/verification/documents/${id}/complete`),

  removeDocument: (id: number) => apiDelete<void>(`/api/v1/me/verification/documents/${id}`),

  startAvatarUpload: (contentType: string, sizeBytes: number) =>
    apiPost<UploadTicket>('/api/v1/me/avatar', { contentType, sizeBytes }),

  completeAvatarUpload: (uploadId: string) =>
    apiPost<void>('/api/v1/me/avatar/complete', { uploadId }),

  removeAvatar: () => apiDelete<void>('/api/v1/me/avatar'),
};

/**
 * Anyone holding a staff role can open the admin portal; which sections they then see is a
 * permission each (see features/admin/permissions.ts). The API checks every call itself, so this
 * only decides what is worth showing.
 */
export function isStaff(
  profile: Pick<Profile, 'permissions' | 'isSuperAdmin'> | undefined,
): boolean {
  return profile?.isSuperAdmin === true || (profile?.permissions ?? []).length > 0;
}

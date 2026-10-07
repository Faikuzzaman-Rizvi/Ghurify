import { apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type VerificationQueuePage = components['schemas']['VerificationQueuePage'];
export type VerificationQueueItem = components['schemas']['VerificationQueueItem'];
export type ReviewVerificationCommand = components['schemas']['ReviewVerificationCommand'];
export type AuditEntry = components['schemas']['AuditEntry'];
export type ReviewDocumentView = components['schemas']['ReviewDocumentView'];
export type DashboardCounts = components['schemas']['DashboardCounts'];
export type SosBoardItem = components['schemas']['SosBoardItem'];
export type MissedCheckIn = components['schemas']['MissedCheckIn'];
export type ReportView = components['schemas']['ReportView'];
export type ReportKind = components['schemas']['ReportKind'];
export type ReportAction = components['schemas']['ReportAction'];
export type ResolveReportCommand = components['schemas']['ResolveReportCommand'];
export type ChangeDestinationStatusCommand =
  components['schemas']['ChangeDestinationStatusCommand'];
export type PayoutView = components['schemas']['PayoutView'];
export type PayoutStatus = components['schemas']['PayoutStatus'];
export type AdminUserPage = components['schemas']['AdminUserPage'];
export type AdminUserItem = components['schemas']['AdminUserItem'];
export type AdminUserDetail = components['schemas']['AdminUserDetail'];
export type AdminTripPage = components['schemas']['AdminTripPage'];
export type AdminTripItem = components['schemas']['AdminTripItem'];
export type AdminBookingDetail = components['schemas']['AdminBookingDetail'];
export type PaymentHistoryPage = components['schemas']['PaymentHistoryPage'];
export type AdminPaymentDetail = components['schemas']['AdminPaymentDetail'];
export type PaymentStatus = NonNullable<components['schemas']['PaymentStatus']>;
export type EmergencyPointView = components['schemas']['EmergencyPointView'];
export type EmergencyPointEdit = components['schemas']['EmergencyPointEdit'];
export type DestinationRequest = components['schemas']['DestinationRequest'];
export type UserStatus = NonNullable<components['schemas']['UserStatus']>;
export type Role = components['schemas']['Role'];
export type TripStatus = NonNullable<components['schemas']['TripStatus']>;

const withSignal = (signal?: AbortSignal) => (signal ? { signal } : {});

/** The admin desk. Every call is re-checked by the API; the UI only hides what it cannot do. */
export const adminApi = {
  dashboard: (signal?: AbortSignal) =>
    apiGet<DashboardCounts>('/api/v1/admin/dashboard', withSignal(signal)),

  verifications: (status: string, page: number, signal?: AbortSignal) =>
    apiGet<VerificationQueuePage>(
      `/api/v1/admin/verifications?status=${encodeURIComponent(status)}&page=${page}`,
      withSignal(signal),
    ),

  /** Links that work for five minutes. The API records every opening in the audit log. */
  verificationDocuments: (id: number) =>
    apiGet<ReviewDocumentView[]>(`/api/v1/admin/verifications/${id}/documents`),

  reviewVerification: (id: number, command: ReviewVerificationCommand) =>
    apiPost<void>(`/api/v1/admin/verifications/${id}/review`, command),

  sosBoard: (includeResolved: boolean, signal?: AbortSignal) =>
    apiGet<SosBoardItem[]>(
      `/api/v1/admin/sos?includeResolved=${includeResolved}`,
      withSignal(signal),
    ),

  acknowledgeSos: (id: number) => apiPost<void>(`/api/v1/admin/sos/${id}/acknowledge`),

  resolveSos: (id: number) => apiPost<void>(`/api/v1/sos/${id}/resolve`),

  missedCheckIns: (signal?: AbortSignal) =>
    apiGet<MissedCheckIn[]>('/api/v1/admin/check-ins/missed', withSignal(signal)),

  setDestinationStatus: (slug: string, command: ChangeDestinationStatusCommand) =>
    apiPost<void>(`/api/v1/admin/destinations/${encodeURIComponent(slug)}/status`, command),

  reports: (kind: ReportKind | null, signal?: AbortSignal) =>
    apiGet<ReportView[]>(
      kind ? `/api/v1/admin/reports?kind=${kind}` : '/api/v1/admin/reports',
      withSignal(signal),
    ),

  resolveReport: (id: number, command: ResolveReportCommand) =>
    apiPost<void>(`/api/v1/admin/reports/${id}/resolve`, command),

  payouts: (status: PayoutStatus, signal?: AbortSignal) =>
    apiGet<PayoutView[]>(`/api/v1/admin/payouts?status=${status}`, withSignal(signal)),

  approvePayout: (id: number) => apiPost<void>(`/api/v1/admin/payouts/${id}/approve`),

  audit: (signal?: AbortSignal) => apiGet<AuditEntry[]>('/api/v1/admin/audit', withSignal(signal)),

  users: (search: string, status: UserStatus | '', page: number, signal?: AbortSignal) =>
    apiGet<AdminUserPage>(
      `/api/v1/admin/users?search=${encodeURIComponent(search)}&page=${page}${status ? `&status=${status}` : ''}`,
      withSignal(signal),
    ),

  user: (id: number, signal?: AbortSignal) =>
    apiGet<AdminUserDetail>(`/api/v1/admin/users/${id}`, withSignal(signal)),

  setUserStatus: (id: number, status: UserStatus, reason: string) =>
    apiPost<void>(`/api/v1/admin/users/${id}/status`, { status, reason }),

  requirePasswordReset: (id: number, reason: string) =>
    apiPost<void>(`/api/v1/admin/users/${id}/require-password-reset`, { reason }),

  changeRole: (id: number, role: Role, grant: boolean) =>
    apiPost<void>(`/api/v1/admin/users/${id}/roles`, { role, grant }),

  removeAvatar: (id: number) => apiDelete<void>(`/api/v1/admin/users/${id}/avatar`),

  trips: (search: string, status: TripStatus | '', page: number, signal?: AbortSignal) =>
    apiGet<AdminTripPage>(
      `/api/v1/admin/trips?search=${encodeURIComponent(search)}&page=${page}${status ? `&status=${status}` : ''}`,
      withSignal(signal),
    ),

  cancelTrip: (id: number, reason: string) =>
    apiPost<void>(`/api/v1/admin/trips/${id}/cancel`, { reason }),

  lookupBooking: (query: string, signal?: AbortSignal) =>
    apiGet<AdminBookingDetail>(
      `/api/v1/admin/bookings/lookup?q=${encodeURIComponent(query)}`,
      withSignal(signal),
    ),

  retryRefunds: () => apiPost<{ count: number | string }>('/api/v1/admin/refunds/retry'),

  /** Every payment, by number, transaction or gateway reference, traveller email or name, or trip. */
  payments: (search: string, status: PaymentStatus | '', page: number, signal?: AbortSignal) =>
    apiGet<PaymentHistoryPage>(
      `/api/v1/admin/payments?search=${encodeURIComponent(search)}&page=${page}${status ? `&status=${status}` : ''}`,
      withSignal(signal),
    ),

  payment: (id: number, signal?: AbortSignal) =>
    apiGet<AdminPaymentDetail>(`/api/v1/admin/payments/${id}`, withSignal(signal)),

  saveDestination: (slug: string, request: DestinationRequest) =>
    apiPut<{ slug: string; added: boolean }>(
      `/api/v1/admin/destinations/${encodeURIComponent(slug)}`,
      request,
    ),

  emergencyPoints: (signal?: AbortSignal) =>
    apiGet<EmergencyPointView[]>('/api/v1/admin/emergency-points', withSignal(signal)),

  saveEmergencyPoint: (edit: EmergencyPointEdit) =>
    apiPost<{ id: number | string }>('/api/v1/admin/emergency-points', edit),

  auditLog: (entityType: string, entityId: string, signal?: AbortSignal) =>
    apiGet<AuditEntry[]>(
      `/api/v1/admin/audit?${new URLSearchParams({ ...(entityType ? { entityType } : {}), ...(entityId ? { entityId } : {}) }).toString()}`,
      withSignal(signal),
    ),
};

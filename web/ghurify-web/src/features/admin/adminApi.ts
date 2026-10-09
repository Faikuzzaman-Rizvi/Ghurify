import { apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';
import { stepUpHeaders } from './stepUp';

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
export type AuditPage = components['schemas']['AuditPage'];
export type AuditChange = components['schemas']['AuditChange'];
export type StaffRolesView = components['schemas']['StaffRolesView'];
export type StaffRoleView = components['schemas']['StaffRoleView'];
export type PermissionView = components['schemas']['PermissionView'];
export type PermissionGroup = components['schemas']['PermissionGroup'];
export type SaveStaffRoleCommand = components['schemas']['SaveStaffRoleCommand'];
export type StaffMembersView = components['schemas']['StaffMembersView'];
export type StaffMemberView = components['schemas']['StaffMemberView'];
export type StaffRoleHeld = components['schemas']['StaffRoleHeld'];
export type StepUpResponse = components['schemas']['StepUpResponse'];
export type SettingsView = components['schemas']['SettingsView'];
export type SettingView = components['schemas']['SettingView'];
export type SettingGroup = components['schemas']['SettingGroup'];
export type SettingKind = components['schemas']['SettingKind'];
export type SiteAssetView = components['schemas']['SiteAssetView'];
export type SettingChangeRecord = components['schemas']['SettingChangeRecord'];
export type SettingsSaved = components['schemas']['SettingsSaved'];
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

  /** Every filter is optional; an action ending in a dot ("payout.") matches that whole group. */
  auditLog: (filter: AuditFilter, page: number, signal?: AbortSignal) =>
    apiGet<AuditPage>(`/api/v1/admin/audit?${auditQuery(filter, page)}`, withSignal(signal)),

  // --- Super admin: the desk's own roles and members ---

  staffRoles: (signal?: AbortSignal) =>
    apiGet<StaffRolesView>('/api/v1/admin/staff/roles', withSignal(signal)),

  /** Needs a step-up receipt; without one the API answers 403 step_up_required. */
  createStaffRole: (command: SaveStaffRoleCommand) =>
    apiPost<StaffRoleView>('/api/v1/admin/staff/roles', command, { headers: stepUpHeaders() }),

  updateStaffRole: (id: number, command: SaveStaffRoleCommand) =>
    apiPut<StaffRoleView>(`/api/v1/admin/staff/roles/${id}`, command, { headers: stepUpHeaders() }),

  deleteStaffRole: (id: number) =>
    apiDelete<void>(`/api/v1/admin/staff/roles/${id}`, { headers: stepUpHeaders() }),

  staffMembers: (signal?: AbortSignal) =>
    apiGet<StaffMembersView>('/api/v1/admin/staff/members', withSignal(signal)),

  assignStaffRole: (userId: number, staffRoleId: number, grant: boolean) =>
    apiPost<void>(
      `/api/v1/admin/staff/members/${userId}`,
      { staffRoleId, grant },
      { headers: stepUpHeaders() },
    ),

  // --- The site's own settings: branding and theme ---

  settings: (signal?: AbortSignal) =>
    apiGet<SettingsView>('/api/v1/admin/settings', withSignal(signal)),

  /** Only the keys that changed. An empty value puts one back to the shipped default. */
  saveSettings: (settings: Record<string, string>) =>
    apiPut<SettingsSaved>('/api/v1/admin/settings', { settings }),

  settingHistory: (key: string, signal?: AbortSignal) =>
    apiGet<SettingChangeRecord[]>(`/api/v1/admin/settings/history/${encodeURIComponent(key)}`, withSignal(signal)),

  /** The image as base64, which the API checks is really an image before keeping it. */
  uploadSiteAsset: (kind: string, contentType: string, base64: string) =>
    apiPut<void>(`/api/v1/admin/settings/assets/${encodeURIComponent(kind)}`, { contentType, base64 }),

  removeSiteAsset: (kind: string) =>
    apiDelete<void>(`/api/v1/admin/settings/assets/${encodeURIComponent(kind)}`),

  /** Confirms the caller's own password and returns the receipt for the calls above. */
  stepUp: (password: string) => apiPost<StepUpResponse>('/api/v1/admin/step-up', { password }),
};

/** What the audit viewer is filtered by. Empty strings mean "no filter". */
export interface AuditFilter {
  actorId: string;
  action: string;
  entityType: string;
  entityId: string;
  from: string;
  to: string;
}

function auditQuery(filter: AuditFilter, page: number): string {
  const query = new URLSearchParams({ page: String(page) });
  if (filter.actorId) query.set('actorId', filter.actorId);
  if (filter.action) query.set('action', filter.action);
  if (filter.entityType) query.set('entityType', filter.entityType);
  if (filter.entityId) query.set('entityId', filter.entityId);
  if (filter.from) query.set('from', `${filter.from}T00:00:00Z`);
  // The API reads 'to' as exclusive, so the chosen day itself is included by asking for
  // everything before the day after it.
  if (filter.to) {
    const dayAfter = new Date(`${filter.to}T00:00:00Z`);
    dayAfter.setUTCDate(dayAfter.getUTCDate() + 1);
    query.set('to', dayAfter.toISOString());
  }
  return query.toString();
}

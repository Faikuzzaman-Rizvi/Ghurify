import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type BookingCheckout = components['schemas']['BookingCheckout'];
export type PaymentStarted = components['schemas']['PaymentStarted'];
export type SandboxPayment = components['schemas']['SandboxPayment'];
export type CallbackHandled = components['schemas']['CallbackHandled'];
export type RefundView = components['schemas']['RefundView'];
export type PaymentHistoryPage = components['schemas']['PaymentHistoryPage'];
export type PaymentHistoryItem = components['schemas']['PaymentHistoryItem'];
export type PaymentDetail = components['schemas']['PaymentDetail'];
export type PaymentRefundView = components['schemas']['PaymentRefundView'];
export type ReceivedPaymentPage = components['schemas']['ReceivedPaymentPage'];
export type PaymentStatus = NonNullable<components['schemas']['PaymentStatus']>;
export type PaymentMethodType = NonNullable<components['schemas']['PaymentMethodType']>;

/** The ways to pay the sandbox page offers; the API reports the choice like a real gateway. */
export const sandboxMethods = ['bkash', 'nagad', 'rocket', 'card'] as const;
export type SandboxMethod = (typeof sandboxMethods)[number];

const withSignal = (signal?: AbortSignal) => (signal ? { signal } : {});

/**
 * Paying into escrow. The amount is never sent: the server takes it from the booking. Every start
 * carries an idempotency key, so a double click or a retry cannot open a second payment.
 */
export const paymentsApi = {
  checkout: (bookingId: number, signal?: AbortSignal) =>
    apiGet<BookingCheckout>(`/api/v1/bookings/${bookingId}/checkout`, withSignal(signal)),

  start: (bookingId: number, idempotencyKey: string) =>
    apiPost<PaymentStarted>(`/api/v1/bookings/${bookingId}/payments`, undefined, {
      headers: { 'Idempotency-Key': idempotencyKey },
    }),

  sandbox: (reference: string, signal?: AbortSignal) =>
    apiGet<SandboxPayment>(
      `/api/v1/payments/sandbox/${encodeURIComponent(reference)}`,
      withSignal(signal),
    ),

  completeSandbox: (reference: string, succeed: boolean, method: SandboxMethod) =>
    apiPost<CallbackHandled>(`/api/v1/payments/sandbox/${encodeURIComponent(reference)}/complete`, {
      succeed,
      method,
    }),

  refunds: (signal?: AbortSignal) => apiGet<RefundView[]>('/api/v1/me/refunds', withSignal(signal)),

  /** The signed-in traveller's payment attempts, newest first, with overall totals. */
  history: (status: PaymentStatus | '', page: number, signal?: AbortSignal) =>
    apiGet<PaymentHistoryPage>(
      `/api/v1/me/payments?page=${page}${status ? `&status=${status}` : ''}`,
      withSignal(signal),
    ),

  /** One of their payments, as a receipt. */
  payment: (id: number, signal?: AbortSignal) =>
    apiGet<PaymentDetail>(`/api/v1/me/payments/${id}`, withSignal(signal)),

  /** A host's paid bookings, optionally for one trip. */
  received: (tripId: number | null, page: number, signal?: AbortSignal) =>
    apiGet<ReceivedPaymentPage>(
      `/api/v1/me/received-payments?page=${page}${tripId ? `&tripId=${tripId}` : ''}`,
      withSignal(signal),
    ),
};

/** A fresh key per attempt: the server treats a repeated key as the same payment. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID().replace(/-/g, '');
}

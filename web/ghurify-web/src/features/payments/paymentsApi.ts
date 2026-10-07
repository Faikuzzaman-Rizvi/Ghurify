import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type BookingCheckout = components['schemas']['BookingCheckout'];
export type PaymentStarted = components['schemas']['PaymentStarted'];
export type SandboxPayment = components['schemas']['SandboxPayment'];
export type CallbackHandled = components['schemas']['CallbackHandled'];
export type RefundView = components['schemas']['RefundView'];

/**
 * Paying into escrow. The amount is never sent: the server takes it from the booking. Every start
 * carries an idempotency key, so a double click or a retry cannot open a second payment.
 */
export const paymentsApi = {
  checkout: (bookingId: number, signal?: AbortSignal) =>
    apiGet<BookingCheckout>(`/api/v1/bookings/${bookingId}/checkout`, signal ? { signal } : {}),

  start: (bookingId: number, idempotencyKey: string) =>
    apiPost<PaymentStarted>(`/api/v1/bookings/${bookingId}/payments`, undefined, {
      headers: { 'Idempotency-Key': idempotencyKey },
    }),

  sandbox: (reference: string, signal?: AbortSignal) =>
    apiGet<SandboxPayment>(
      `/api/v1/payments/sandbox/${encodeURIComponent(reference)}`,
      signal ? { signal } : {},
    ),

  completeSandbox: (reference: string, succeed: boolean) =>
    apiPost<CallbackHandled>(`/api/v1/payments/sandbox/${encodeURIComponent(reference)}/complete`, {
      succeed,
    }),

  refunds: (signal?: AbortSignal) =>
    apiGet<RefundView[]>('/api/v1/me/refunds', signal ? { signal } : {}),
};

/** A fresh key per attempt: the server treats a repeated key as the same payment. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID().replace(/-/g, '');
}

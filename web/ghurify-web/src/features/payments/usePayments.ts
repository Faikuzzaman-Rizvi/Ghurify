import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/features/auth/authStore';
import { paymentsApi, type PaymentStatus } from './paymentsApi';

function useUserKey(): number | 'anonymous' {
  return useAuthStore((state) => state.user?.id ?? 'anonymous');
}

/** The signed-in traveller's payment history. Keyed by user, so a sign-out never shows it to the next person. */
export function usePaymentHistory(status: PaymentStatus | '', page: number) {
  const user = useUserKey();

  return useQuery({
    queryKey: ['me', user, 'payments', status, page],
    queryFn: ({ signal }) => paymentsApi.history(status, page, signal),
    placeholderData: (previous) => previous,
  });
}

export function usePaymentReceipt(id: number) {
  const user = useUserKey();

  return useQuery({
    queryKey: ['me', user, 'payment', id],
    queryFn: ({ signal }) => paymentsApi.payment(id, signal),
    enabled: Number.isFinite(id) && id > 0,
  });
}

export function useReceivedPayments(tripId: number | null, page: number) {
  const user = useUserKey();

  return useQuery({
    queryKey: ['host', user, 'received-payments', tripId, page],
    queryFn: ({ signal }) => paymentsApi.received(tripId, page, signal),
    placeholderData: (previous) => previous,
  });
}

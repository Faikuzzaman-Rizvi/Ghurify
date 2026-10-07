import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/features/auth/authStore';
import { bookingsApi } from './bookingsApi';

function useUserKey(): number | 'anonymous' {
  return useAuthStore((state) => state.user?.id ?? 'anonymous');
}

export function useMyBookings() {
  const user = useUserKey();

  return useQuery({
    queryKey: ['me', user, 'bookings'],
    queryFn: ({ signal }) => bookingsApi.mine(signal),
  });
}

export function useTripRequests(tripId: number) {
  const user = useUserKey();

  return useQuery({
    queryKey: ['host', user, 'requests', tripId],
    queryFn: ({ signal }) => bookingsApi.forTrip(tripId, signal),
    enabled: Number.isFinite(tripId) && tripId > 0,
  });
}

/** Seats and statuses change with every booking action; refresh everything that shows them. */
function useInvalidateBookings() {
  const queryClient = useQueryClient();
  const user = useUserKey();

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['me', user] }),
      queryClient.invalidateQueries({ queryKey: ['host', user] }),
      queryClient.invalidateQueries({ queryKey: ['trips'] }),
    ]);
}

export function useRequestToJoin(tripId: number) {
  const invalidate = useInvalidateBookings();

  return useMutation({
    mutationFn: (message: string) => bookingsApi.requestToJoin(tripId, message),
    onSuccess: () => invalidate(),
  });
}

export function useCancelRequest() {
  const invalidate = useInvalidateBookings();

  return useMutation({
    mutationFn: (requestId: number) => bookingsApi.cancel(requestId),
    onSuccess: () => invalidate(),
  });
}

export function useApproveRequest() {
  const invalidate = useInvalidateBookings();

  return useMutation({
    mutationFn: (requestId: number) => bookingsApi.approve(requestId),
    onSuccess: () => invalidate(),
  });
}

export function useDeclineRequest() {
  const invalidate = useInvalidateBookings();

  return useMutation({
    mutationFn: (requestId: number) => bookingsApi.decline(requestId),
    onSuccess: () => invalidate(),
  });
}

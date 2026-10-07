import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/features/auth/authStore';
import { hostApi, type SaveTripCommand } from './hostApi';

function useHostKey(): number | 'anonymous' {
  return useAuthStore((state) => state.user?.id ?? 'anonymous');
}

export function useMyHostedTrips() {
  const host = useHostKey();

  return useQuery({
    queryKey: ['host', host, 'trips'],
    queryFn: ({ signal }) => hostApi.myTrips(signal),
  });
}

/** After any write, the host's list and every cached trip page may be stale. */
function useInvalidateTrips() {
  const queryClient = useQueryClient();
  const host = useHostKey();

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['host', host] }),
      queryClient.invalidateQueries({ queryKey: ['trips'] }),
    ]);
}

export function useCreateTrip() {
  const invalidate = useInvalidateTrips();

  return useMutation({
    mutationFn: (command: SaveTripCommand) => hostApi.create(command),
    onSuccess: () => invalidate(),
  });
}

export function useUpdateTrip(id: number) {
  const invalidate = useInvalidateTrips();

  return useMutation({
    mutationFn: (command: SaveTripCommand) => hostApi.update(id, command),
    onSuccess: () => invalidate(),
  });
}

/** Cancels one of the host's trips; every paid traveller is refunded in full by the API. */
export function useCancelTrip(id: number) {
  const invalidate = useInvalidateTrips();

  return useMutation({
    mutationFn: () => hostApi.cancel(id),
    onSuccess: () => invalidate(),
  });
}

export function usePublishTrip() {
  const invalidate = useInvalidateTrips();

  return useMutation({
    mutationFn: (id: number) => hostApi.publish(id),
    onSuccess: () => invalidate(),
  });
}

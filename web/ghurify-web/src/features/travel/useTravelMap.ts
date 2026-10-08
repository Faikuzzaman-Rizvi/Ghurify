import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/features/auth/authStore';
import { travelApi, type AddVisitCommand, type EditVisitCommand } from './travelApi';

/**
 * The signed-in person's map. Under ['me', …]: a live notification (a trip just completed puts it
 * on the map) refreshes it like everything else that is theirs.
 */
function useMyMapKey() {
  const userKey = useAuthStore((state) => state.user?.id ?? 'anonymous');
  return ['me', userKey, 'travel-map'] as const;
}

export function useMyTravelMap() {
  const status = useAuthStore((state) => state.status);
  const key = useMyMapKey();

  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => travelApi.mine(signal),
    enabled: status === 'authenticated',
    // Photos just uploaded are checked on the server first: look again until they are ready.
    refetchInterval: (query) =>
      query.state.data?.places.some((place) =>
        place.visits.some((visit) => Number(visit.photosProcessing) > 0),
      )
        ? 3000
        : false,
  });
}

export function useSharedTravelMap(userId: number) {
  return useQuery({
    queryKey: ['travel-map', userId],
    queryFn: ({ signal }) => travelApi.shared(userId, signal),
    enabled: userId > 0,
    staleTime: 5 * 60_000,
  });
}

/** Every change to the map refetches it: the server groups visits into pins and sums them up. */
function useMapChange<T, R>(change: (value: T) => Promise<R>) {
  const queryClient = useQueryClient();
  const key = useMyMapKey();

  return useMutation({
    mutationFn: change,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  });
}

export function useAddVisit() {
  return useMapChange((command: AddVisitCommand) => travelApi.addVisit(command));
}

export function useEditVisit() {
  return useMapChange(({ id, command }: { id: number; command: EditVisitCommand }) =>
    travelApi.editVisit(id, command),
  );
}

export function useRemoveVisit() {
  return useMapChange((id: number) => travelApi.removeVisit(id));
}

export function useAddVisitPhotos() {
  return useMapChange(({ visitId, mediaIds }: { visitId: number; mediaIds: number[] }) =>
    travelApi.addPhotos(visitId, mediaIds),
  );
}

export function useRemoveVisitPhoto() {
  return useMapChange(({ visitId, mediaId }: { visitId: number; mediaId: number }) =>
    travelApi.removePhoto(visitId, mediaId),
  );
}

export function useTravelMapSharing() {
  return useMapChange((share: boolean) => travelApi.setSharing(share));
}

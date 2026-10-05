import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@/features/auth/authStore';
import { tripsApi, type TripFilters } from './tripsApi';

/**
 * The signed-in user is part of every key: what a search returns depends on who asks
 * (women-only trips), so signing in or out must not serve the other person's cached answer.
 */
function useViewerKey(): number | 'anonymous' {
  return useAuthStore((state) => state.user?.id ?? 'anonymous');
}

export function useTripSearch(filters: TripFilters) {
  const viewer = useViewerKey();

  return useQuery({
    queryKey: ['trips', 'search', viewer, filters],
    queryFn: ({ signal }) => tripsApi.search(filters, signal),
    // Keeps the current cards on screen while the next page or filter loads.
    placeholderData: keepPreviousData,
  });
}

export function useTrip(id: number) {
  const viewer = useViewerKey();

  return useQuery({
    queryKey: ['trips', 'detail', viewer, id],
    queryFn: ({ signal }) => tripsApi.get(id, signal),
    enabled: Number.isFinite(id) && id > 0,
  });
}

export function useDestinations() {
  const viewer = useViewerKey();

  return useQuery({
    queryKey: ['destinations', viewer],
    queryFn: ({ signal }) => tripsApi.destinations(signal),
    // Destinations change rarely; status changes come through alerts, not this list.
    staleTime: 5 * 60_000,
  });
}

export function useDestination(slug: string) {
  const viewer = useViewerKey();

  return useQuery({
    queryKey: ['destinations', viewer, slug],
    queryFn: ({ signal }) => tripsApi.destination(slug, signal),
    enabled: slug.length > 0,
  });
}

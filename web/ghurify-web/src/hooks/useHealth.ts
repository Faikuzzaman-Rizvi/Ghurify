import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';

/** Taken from the generated OpenAPI types; never hand-written. Regenerate with `npm run gen:api`. */
export type HealthResponse = components['schemas']['HealthResponse'];

export const healthQueryKey = ['health'] as const;

/**
 * Asks the API whether it is up and can reach the database.
 * Not retried: when the API is down the user wants to be told now, not in twenty seconds.
 */
export function useHealth() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: ({ signal }) => apiGet<HealthResponse>('/api/v1/health', { signal }),
    retry: false,
    staleTime: 10_000,
  });
}

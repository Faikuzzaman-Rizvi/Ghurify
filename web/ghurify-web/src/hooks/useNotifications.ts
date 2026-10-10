import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';
import { useAuthStore } from '@/features/auth/authStore';

export type NotificationItem = components['schemas']['NotificationItem'];
export type NotificationPage = components['schemas']['NotificationPage'];

function useNotificationsKey() {
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');
  return ['me', user, 'notifications'] as const;
}

/** The bell's list and unread count. Kept fresh by {@link useLiveNotifications}. */
export function useNotifications() {
  const status = useAuthStore((state) => state.status);
  const key = useNotificationsKey();

  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => apiGet<NotificationPage>('/api/v1/me/notifications', { signal }),
    enabled: status === 'authenticated',
    // The hub pushes changes; this only catches up after a long disconnect.
    refetchInterval: 5 * 60_000,
  });
}

/**
 * One live connection to /hubs/notify while signed in, for the whole app: mounted once at the
 * root, so moving between the site and the admin portal (each with its own bell) never drops and
 * reopens it.
 */
export function useLiveNotifications() {
  const status = useAuthStore((state) => state.status);
  const queryClient = useQueryClient();

  useEffect(() => {
    if (status !== 'authenticated') {
      return;
    }

    // The SignalR client is fetched here rather than imported at the top of the file. It is 15 kB
    // compressed and is only ever used by somebody signed in, so an anonymous visitor reading the
    // home page no longer downloads it to not use it.
    let stop: (() => void) | undefined;
    let cancelled = false;

    void import('@/api/realtime').then(({ createHubConnection, runHubConnection }) => {
      // Signed out again, or the component unmounted, while the client was downloading.
      if (cancelled) {
        return;
      }

      const connection = createHubConnection('/hubs/notify');

      // A new notification can mean a new request, an approval, a released seat: refresh the bell
      // and whatever lists might show the change.
      connection.on('notification', () => {
        void queryClient.invalidateQueries({ queryKey: ['me'] });
        void queryClient.invalidateQueries({ queryKey: ['host'] });
      });

      // Offline or the hub is down: the bell still works from the periodic refetch.
      stop = runHubConnection(connection);
    });

    return () => {
      cancelled = true;
      stop?.();
    };
  }, [status, queryClient]);
}

export function useMarkNotificationsRead() {
  const queryClient = useQueryClient();
  const key = useNotificationsKey();

  return useMutation({
    mutationFn: (upToId: number) => apiPost<void>('/api/v1/me/notifications/read', { upToId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  });
}

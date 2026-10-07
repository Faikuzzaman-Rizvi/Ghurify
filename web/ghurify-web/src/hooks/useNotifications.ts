import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';
import { createHubConnection } from '@/api/realtime';
import { useAuthStore } from '@/features/auth/authStore';

export type NotificationItem = components['schemas']['NotificationItem'];
export type NotificationPage = components['schemas']['NotificationPage'];

function useNotificationsKey() {
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');
  return ['me', user, 'notifications'] as const;
}

/** The bell's list and unread count, refreshed live from /hubs/notify while signed in. */
export function useNotifications() {
  const status = useAuthStore((state) => state.status);
  const queryClient = useQueryClient();
  const key = useNotificationsKey();

  const query = useQuery({
    queryKey: key,
    queryFn: ({ signal }) => apiGet<NotificationPage>('/api/v1/me/notifications', { signal }),
    enabled: status === 'authenticated',
    // The hub pushes changes; this only catches up after a long disconnect.
    refetchInterval: 5 * 60_000,
  });

  useEffect(() => {
    if (status !== 'authenticated') {
      return;
    }

    const connection = createHubConnection('/hubs/notify');

    // A new notification can mean a new request, an approval, a released seat: refresh the bell
    // and whatever lists might show the change.
    connection.on('notification', () => {
      void queryClient.invalidateQueries({ queryKey: ['me'] });
      void queryClient.invalidateQueries({ queryKey: ['host'] });
    });

    connection.start().catch(() => {
      // Offline or the hub is down: the bell still works from the periodic refetch.
    });

    return () => {
      void connection.stop();
    };
  }, [status, queryClient]);

  return query;
}

export function useMarkNotificationsRead() {
  const queryClient = useQueryClient();
  const key = useNotificationsKey();

  return useMutation({
    mutationFn: (upToId: number) => apiPost<void>('/api/v1/me/notifications/read', { upToId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  });
}

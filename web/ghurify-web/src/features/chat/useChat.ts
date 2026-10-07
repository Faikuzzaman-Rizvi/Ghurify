import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, asNumber } from '@/api/client';
import { createHubConnection, runHubConnection } from '@/api/realtime';
import { useAuthStore } from '@/features/auth/authStore';
import { chatApi, type ChatMessage } from './chatApi';

export type Connection = 'connecting' | 'live' | 'reconnecting' | 'offline';

const pageSize = 50;

/** Newest last, no duplicates: a message can arrive over the hub and in a page fetch. */
function merge(...lists: ChatMessage[][]): ChatMessage[] {
  const byId = new Map<number, ChatMessage>();
  for (const message of lists.flat()) {
    byId.set(asNumber(message.id), message);
  }
  return [...byId.values()].sort((a, b) => asNumber(a.id) - asNumber(b.id));
}

/**
 * A trip's chat: the latest page, older pages loaded on request, live messages from the hub, the
 * connection state for the "reconnecting" banner, and the read marker kept up to date.
 */
export function useTripChat(tripId: number) {
  const [older, setOlder] = useState<ChatMessage[]>([]);
  const [olderExhausted, setOlderExhausted] = useState(false);
  const [live, setLive] = useState<ChatMessage[]>([]);
  const [connection, setConnection] = useState<Connection>('connecting');
  const [notMember, setNotMember] = useState(false);
  const lastMarked = useRef(0);
  const queryClient = useQueryClient();

  const latest = useQuery({
    queryKey: ['chat', tripId, 'latest'],
    queryFn: ({ signal }) => chatApi.history(tripId, undefined, signal),
    enabled: tripId > 0,
    refetchOnWindowFocus: false,
  });

  const messages = useMemo(
    () => merge(older, latest.data?.messages ?? [], live),
    [older, latest.data, live],
  );

  const pinned = useMemo(
    () =>
      merge(
        latest.data?.pinned ?? [],
        live.filter((message) => message.isPinned),
      ).reverse(),
    [latest.data, live],
  );

  // Only members can open the chat, and the history request is what says so: until it has
  // answered there is nothing to join, and asking the hub anyway just gets a refusal.
  const isMember = latest.isSuccess;

  useEffect(() => {
    if (tripId <= 0 || !isMember) return;

    const hub = createHubConnection('/hubs/chat');
    hub.on('message', (message: ChatMessage) => setLive((current) => [...current, message]));
    hub.onreconnecting(() => setConnection('reconnecting'));
    // A reconnect is a new connection: join the group again and fetch what was missed.
    hub.onreconnected(() => {
      setConnection('live');
      void hub.invoke('JoinTrip', tripId).catch(() => setNotMember(true));
      void queryClient.invalidateQueries({ queryKey: ['chat', tripId, 'latest'] });
    });
    hub.onclose(() => setConnection('offline'));

    let active = true;
    const stop = runHubConnection(hub, {
      onStarted: () =>
        void hub.invoke('JoinTrip', tripId).then(
          () => {
            if (active) setConnection('live');
          },
          () => {
            if (!active) return;
            setNotMember(true);
            setConnection('offline');
          },
        ),
      onFailed: () => setConnection('offline'),
    });

    return () => {
      active = false;
      stop();
    };
  }, [tripId, isMember, queryClient]);

  // Mark read up to the newest message on screen.
  const newest = messages.at(-1);
  useEffect(() => {
    if (!newest) return;
    const id = asNumber(newest.id);
    if (id > lastMarked.current) {
      lastMarked.current = id;
      void chatApi
        .markRead(tripId, id)
        .then(() => queryClient.invalidateQueries({ queryKey: ['me'] }))
        .catch(() => undefined);
    }
  }, [newest, tripId, queryClient]);

  const loadOlder = useCallback(async () => {
    const oldest = messages[0];
    if (!oldest) return;
    const page = await chatApi.history(tripId, asNumber(oldest.id));
    setOlder((current) => [...current, ...page.messages]);
    setOlderExhausted(page.messages.length < pageSize);
  }, [messages, tripId]);

  const notFound = notMember || (latest.error instanceof ApiError && latest.error.status === 404);

  return {
    messages,
    pinned,
    connection,
    hasOlder: !olderExhausted && (latest.data?.messages.length ?? 0) >= pageSize,
    loadOlder,
    isLoading: latest.isPending,
    isError: latest.isError && !notFound,
    notFound,
    retry: () => void latest.refetch(),
    add: (message: ChatMessage) => setLive((current) => [...current, message]),
  };
}

/** Unread counts across the signed-in user's chats. */
export function useChatUnread() {
  const status = useAuthStore((state) => state.status);
  const user = useAuthStore((state) => state.user?.id ?? 'anonymous');

  return useQuery({
    queryKey: ['me', user, 'chats'],
    queryFn: ({ signal }) => chatApi.unread(signal),
    enabled: status === 'authenticated',
    refetchInterval: 60_000,
  });
}

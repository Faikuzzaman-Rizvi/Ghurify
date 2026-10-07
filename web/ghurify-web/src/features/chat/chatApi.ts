import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';

/** All taken from the generated OpenAPI types. Regenerate with `npm run gen:api`. */
export type ChatMessage = components['schemas']['ChatMessageView'];
export type ChatHistory = components['schemas']['ChatHistory'];
export type ChatMessageSent = components['schemas']['ChatMessageSent'];
export type ChatUnread = components['schemas']['ChatUnread'];

/** A trip's group chat over HTTP. Live messages arrive over /hubs/chat. */
export const chatApi = {
  history: (tripId: number, before?: number, signal?: AbortSignal) =>
    apiGet<ChatHistory>(
      `/api/v1/trips/${tripId}/chat${before ? `?before=${before}` : ''}`,
      signal ? { signal } : {},
    ),

  send: (tripId: number, body: string, pin = false) =>
    apiPost<ChatMessageSent>(`/api/v1/trips/${tripId}/chat`, { body, pin }),

  markRead: (tripId: number, lastReadId: number) =>
    apiPost<void>(`/api/v1/trips/${tripId}/chat/read`, { lastReadId }),

  unread: (signal?: AbortSignal) =>
    apiGet<ChatUnread[]>('/api/v1/me/chats', signal ? { signal } : {}),
};

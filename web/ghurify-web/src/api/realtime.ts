import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { getAccessToken } from './client';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * A SignalR connection to one of the API's hubs (/hubs/notify, /hubs/chat, /hubs/safety).
 *
 * The access token is read on every (re)connect rather than captured once, so a connection that
 * outlives one 15-minute token picks up the silently refreshed one. Reconnects automatically,
 * backing off, because mobile connections drop all the time.
 */
export function createHubConnection(path: string): HubConnection {
  return (
    new HubConnectionBuilder()
      // Absolute, so the client resolves it the same way in every browser (and in tests).
      .withUrl(new URL(path, baseUrl || window.location.origin).toString(), {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build()
  );
}

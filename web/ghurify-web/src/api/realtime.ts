import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
  type ILogger,
} from '@microsoft/signalr';
import { getAccessToken } from './client';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * Stopping a connection that is still starting is what leaving a screen does, and SignalR reports
 * that abandoned handshake as an error ("The connection was stopped during negotiation"), though
 * nothing went wrong. Those lines are dropped; real failures still reach the console.
 */
const abandonedStart = /stopped during negotiation|before stop\(\) was called/i;

/** Set once the page is being left (a reload, a typed address): its requests are cut off then. */
let leavingPage = false;
if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', () => (leavingPage = true));
  window.addEventListener('pageshow', () => (leavingPage = false));
}

const logger: ILogger = {
  log(level, message) {
    if (level < LogLevel.Warning || leavingPage || abandonedStart.test(message)) {
      return;
    }
    if (level >= LogLevel.Error) {
      console.error(`[realtime] ${message}`);
    } else {
      console.warn(`[realtime] ${message}`);
    }
  },
};

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
      .configureLogging(logger)
      .build()
  );
}

/**
 * Starts `connection` and returns the function that stops it: call it from an effect and return
 * the result as the cleanup.
 *
 * The start waits one tick. React StrictMode (in development) mounts every effect, cleans it up
 * and mounts it again at once, and a quick click through the site does much the same; the first
 * connection then never begins a handshake it would have to abandon. After the stop, neither
 * callback runs, so a screen that has gone never sets its state.
 */
export function runHubConnection(
  connection: HubConnection,
  { onStarted, onFailed }: { onStarted?: () => void; onFailed?: (error: unknown) => void } = {},
): () => void {
  let stopped = false;

  const timer = setTimeout(() => {
    connection.start().then(
      () => {
        if (!stopped) onStarted?.();
      },
      (error: unknown) => {
        if (!stopped) onFailed?.(error);
      },
    );
  }, 0);

  return () => {
    stopped = true;
    clearTimeout(timer);
    connection.stop().catch(() => undefined);
  };
}

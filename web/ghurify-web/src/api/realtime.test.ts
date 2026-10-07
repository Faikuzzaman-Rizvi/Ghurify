import { afterEach, describe, expect, it, vi } from 'vitest';
import type { HubConnection } from '@microsoft/signalr';
import { runHubConnection } from './realtime';

function fakeConnection(start: () => Promise<void> = () => Promise.resolve()) {
  return {
    start: vi.fn(start),
    stop: vi.fn(() => Promise.resolve()),
  };
}

describe('runHubConnection', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  // React StrictMode mounts, cleans up and mounts again at once: the first connection must never
  // begin a handshake, or the browser logs "the connection was stopped during negotiation".
  it('never starts a connection that is stopped straight away', async () => {
    vi.useFakeTimers();
    const connection = fakeConnection();

    const stop = runHubConnection(connection as unknown as HubConnection);
    stop();
    await vi.runAllTimersAsync();

    expect(connection.start).not.toHaveBeenCalled();
    expect(connection.stop).toHaveBeenCalledOnce();
  });

  it('starts on the next tick and reports it', async () => {
    vi.useFakeTimers();
    const connection = fakeConnection();
    const onStarted = vi.fn();

    runHubConnection(connection as unknown as HubConnection, { onStarted });
    expect(connection.start).not.toHaveBeenCalled();
    await vi.runAllTimersAsync();

    expect(connection.start).toHaveBeenCalledOnce();
    expect(onStarted).toHaveBeenCalledOnce();
  });

  it('says nothing about a start that fails after the screen has gone', async () => {
    vi.useFakeTimers();
    let fail: (error: Error) => void = () => undefined;
    const connection = fakeConnection(() => new Promise<void>((_, reject) => (fail = reject)));
    const onFailed = vi.fn();

    const stop = runHubConnection(connection as unknown as HubConnection, { onFailed });
    await vi.runAllTimersAsync();
    stop();
    fail(new Error('The connection was stopped during negotiation.'));
    await vi.runAllTimersAsync();

    expect(onFailed).not.toHaveBeenCalled();
  });
});

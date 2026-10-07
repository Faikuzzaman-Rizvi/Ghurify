import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, waitFor } from '@testing-library/react';

import { setAccessToken } from '@/api/client';
import { stubResponse } from '@/test/fetchStub';
import { useAuthStore } from './authStore';
import { useSilentRefresh } from './useSilentRefresh';

const session = {
  accessToken: 'access-token',
  expiresInSeconds: 900,
  user: { id: 7, maskedEmail: 'r****i@example.com', displayName: 'Rizvi' },
};

/** Answers each call with the next response in the list. */
function stubSequence(...responses: ReturnType<typeof stubResponse>[]) {
  const fetchMock = vi.fn((_input: string | URL | Request, _init?: RequestInit) =>
    Promise.resolve(responses.shift() ?? stubResponse({ title: 'Not found' }, 404)),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function KeepsSession() {
  useSilentRefresh();
  return null;
}

describe('useSilentRefresh', () => {
  beforeEach(() => {
    useAuthStore.setState({ status: 'unknown', user: null, expiresAt: null });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    setAccessToken(null);
    useAuthStore.setState({ status: 'unknown', user: null, expiresAt: null });
  });

  // A rate limit or an unreachable API says nothing about the session; treating it as "signed
  // out" threw people out while they clicked around quickly.
  it('keeps trying while the API is busy, instead of signing the person out', async () => {
    vi.useFakeTimers();
    stubSequence(stubResponse({ title: 'Too Many Requests' }, 429), stubResponse(session));

    render(<KeepsSession />);
    await act(() => vi.advanceTimersByTimeAsync(0));
    expect(useAuthStore.getState().status).toBe('unknown');

    await act(() => vi.advanceTimersByTimeAsync(2_000));
    expect(useAuthStore.getState().status).toBe('authenticated');
  });

  it('takes no session (no cookie, a 204) to mean signed out, without an error', async () => {
    stubSequence(stubResponse(undefined, 204));

    render(<KeepsSession />);

    await waitFor(() => expect(useAuthStore.getState().status).toBe('anonymous'));
  });

  it('takes a refused cookie to mean signed out', async () => {
    stubSequence(stubResponse({ title: 'Unauthorized' }, 401));

    render(<KeepsSession />);

    await waitFor(() => expect(useAuthStore.getState().status).toBe('anonymous'));
  });

  // Signing in with the form, not only a session restored on load, must be renewed before its
  // 15-minute token runs out; otherwise every page fails with "signed out" a quarter of an hour in.
  it('renews a session that began by signing in, a minute before its token expires', async () => {
    vi.useFakeTimers();
    const fetchMock = stubSequence(
      stubResponse({ title: 'Unauthorized' }, 401),
      stubResponse({ ...session, accessToken: 'renewed' }),
    );

    render(<KeepsSession />);
    await act(() => vi.advanceTimersByTimeAsync(0));
    expect(useAuthStore.getState().status).toBe('anonymous');

    act(() => useAuthStore.getState().signIn(session));
    await act(() => vi.advanceTimersByTimeAsync(839_000));
    expect(fetchMock).toHaveBeenCalledOnce();

    await act(() => vi.advanceTimersByTimeAsync(1_000));
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(useAuthStore.getState().status).toBe('authenticated');
  });
});

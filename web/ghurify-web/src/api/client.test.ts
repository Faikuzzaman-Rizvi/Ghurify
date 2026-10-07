import { afterEach, describe, expect, it, vi } from 'vitest';
import { stubResponse } from '@/test/fetchStub';
import { ApiError, apiGet, apiPost, setAccessToken, setSessionRenewer } from './client';

/** Answers each call with the next response in the list. */
function stubSequence(...responses: ReturnType<typeof stubResponse>[]) {
  const fetchMock = vi.fn((_input: string | URL | Request, _init?: RequestInit) =>
    Promise.resolve(responses.shift() ?? stubResponse({ title: 'Not found' }, 404)),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function authorizationOf(init: RequestInit | undefined): string | undefined {
  return (init?.headers as Record<string, string> | undefined)?.['Authorization'];
}

describe('API client and an expired access token', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    setAccessToken(null);
    setSessionRenewer(null);
  });

  it('renews the session once and repeats the request with the new token', async () => {
    const fetchMock = stubSequence(
      stubResponse({ title: 'Unauthorized' }, 401),
      stubResponse({ displayName: 'Rizvi' }),
    );
    setAccessToken('expired');
    const renew = vi.fn(() => {
      setAccessToken('fresh');
      return Promise.resolve(true);
    });
    setSessionRenewer(renew);

    const profile = await apiGet<{ displayName: string }>('/api/v1/me/profile');

    expect(profile.displayName).toBe('Rizvi');
    expect(renew).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(authorizationOf(fetchMock.mock.calls[0]?.[1])).toBe('Bearer expired');
    expect(authorizationOf(fetchMock.mock.calls[1]?.[1])).toBe('Bearer fresh');
  });

  it('gives up with the 401 when the session cannot be renewed', async () => {
    const fetchMock = stubSequence(stubResponse({ title: 'Unauthorized' }, 401));
    setAccessToken('expired');
    setSessionRenewer(() => Promise.resolve(false));

    await expect(apiGet('/api/v1/me/profile')).rejects.toMatchObject({ status: 401 });
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('never renews for a request sent without a token', async () => {
    stubSequence(stubResponse({ title: 'Unauthorized', code: 'invalid_credentials' }, 401));
    setAccessToken('fresh');
    const renew = vi.fn(() => Promise.resolve(true));
    setSessionRenewer(renew);

    const error = await apiPost('/api/v1/auth/sign-in', {}, { authenticated: false }).catch(
      (caught: unknown) => caught,
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(renew).not.toHaveBeenCalled();
  });
});

import { afterEach, describe, expect, it, vi } from 'vitest';

import { refreshSession } from './authApi';
import { requests, stubApi } from '@/test/fetchStub';

const session = {
  accessToken: 'access-token',
  expiresInSeconds: 900,
  user: { id: 7, maskedEmail: 'r****i@example.com', displayName: 'Rizvi' },
};

describe('refreshSession', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // Refresh tokens rotate, and the API ends the whole session when one is presented twice. Two
  // refreshes racing with the same cookie (StrictMode mounts, two tabs) would sign people out.
  it('sends one request however many callers ask at once', async () => {
    const fetchMock = stubApi([[/\/api\/v1\/auth\/refresh$/, session]]);

    const [first, second] = await Promise.all([refreshSession(), refreshSession()]);

    expect(first.accessToken).toBe('access-token');
    expect(second).toBe(first);
    expect(
      requests(fetchMock).filter((request) => request.url.endsWith('/auth/refresh')),
    ).toHaveLength(1);
  });

  it('asks again once the previous refresh has finished', async () => {
    const fetchMock = stubApi([[/\/api\/v1\/auth\/refresh$/, session]]);

    await refreshSession();
    await refreshSession();

    expect(
      requests(fetchMock).filter((request) => request.url.endsWith('/auth/refresh')),
    ).toHaveLength(2);
  });
});

import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';

import { LoginPage } from './LoginPage';
import { useAuthStore } from './authStore';
import i18n from '@/i18n';

interface StubResponse {
  ok: boolean;
  status: number;
  statusText: string;
  json: () => Promise<unknown>;
  text: () => Promise<string>;
  headers: Headers;
}

function stub(body: unknown, { ok = true, status = 200 } = {}): StubResponse {
  const text = JSON.stringify(body);
  return {
    ok,
    status,
    statusText: ok ? 'OK' : 'Error',
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(text),
    headers: new Headers({ 'Content-Type': 'application/json' }),
  };
}

function renderLogin() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/login']}>
        <LoginPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('LoginPage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({ status: 'anonymous', user: null });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('rejects an address that is not an email, without calling the API', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'not-an-email');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/valid email address/);
    // Checking the shape in the browser saves a pointless round trip and an email.
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('moves to the code step after the API accepts the address', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(stub({ expiresInSeconds: 300, resendAfterSeconds: 60 })),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    expect(await screen.findByLabelText('Six-digit code')).toBeInTheDocument();
    expect(screen.getByText(/We sent a code to/)).toBeInTheDocument();
  });

  it('disables resend until the countdown has run down', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(stub({ expiresInSeconds: 300, resendAfterSeconds: 60 })),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    // The API sets the wait; the UI must not let the user hammer the send-code endpoint.
    const resend = await screen.findByRole('button', { name: /Send a new code in/ });
    expect(resend).toBeDisabled();
  });

  it('signs the user in when the code is accepted', async () => {
    const session = {
      accessToken: 'test-access-token',
      expiresInSeconds: 900,
      user: { id: 7, maskedEmail: 'r****i@example.com', displayName: null },
    };

    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(stub({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
        .mockResolvedValueOnce(stub(session)),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    await userEvent.type(await screen.findByLabelText('Six-digit code'), '123456');
    await userEvent.click(screen.getByRole('button', { name: 'Verify and sign in' }));

    await waitFor(() => {
      expect(useAuthStore.getState().status).toBe('authenticated');
    });

    expect(useAuthStore.getState().user?.maskedEmail).toBe('r****i@example.com');
  });

  it('shows a usable message when the code is refused', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(stub({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
        .mockResolvedValueOnce(
          stub({ title: 'Sign-in failed', status: 401 }, { ok: false, status: 401 }),
        ),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    await userEvent.type(await screen.findByLabelText('Six-digit code'), '000000');
    await userEvent.click(screen.getByRole('button', { name: 'Verify and sign in' }));

    expect(await screen.findByText(/That code is not valid/)).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('explains a rate limit rather than showing a generic error', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          stub({ title: 'Too many requests', status: 429 }, { ok: false, status: 429 }),
        ),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send code' }));

    expect(await screen.findByText(/Too many codes requested/)).toBeInTheDocument();
  });

  it('renders the sign-in screen in Bangla', async () => {
    await i18n.changeLanguage('bn');
    vi.stubGlobal('fetch', vi.fn());

    renderLogin();

    expect(screen.getByText('ঘুরিফাইতে সাইন ইন করুন')).toBeInTheDocument();
    expect(screen.getByLabelText('ইমেইল ঠিকানা')).toBeInTheDocument();
  });
});

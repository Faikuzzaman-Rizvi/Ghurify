import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactElement } from 'react';

import { HomePage } from './HomePage';
import i18n from '@/i18n';

/** Each test gets its own cache so one test's result cannot leak into the next. */
function renderWithQueryClient(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

interface FetchStub {
  ok: boolean;
  status: number;
  statusText: string;
  json: () => Promise<unknown>;
}

/** Replaces fetch for one render. Defaults to a healthy API; pass overrides for failures. */
function mockFetchOnce(overrides: Partial<FetchStub> = {}) {
  const stub: FetchStub = {
    ok: true,
    status: 200,
    statusText: 'OK',
    json: () => Promise.resolve({ status: 'Healthy', databaseStatus: 'Healthy' }),
    ...overrides,
  };

  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(stub));
}

describe('HomePage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the healthy state once the API answers', async () => {
    mockFetchOnce({});

    renderWithQueryClient(<HomePage />);

    expect(await screen.findByText('API healthy')).toBeInTheDocument();
    expect(screen.getByText(/Database/)).toBeInTheDocument();
  });

  it('shows a retry button when the API cannot be reached', async () => {
    mockFetchOnce({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
      json: () => Promise.resolve({ title: 'Service unavailable', status: 503 }),
    });

    renderWithQueryClient(<HomePage />);

    expect(await screen.findByText('API not reachable')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('renders Bangla text when the language is Bangla', async () => {
    mockFetchOnce({});
    await i18n.changeLanguage('bn');

    renderWithQueryClient(<HomePage />);

    expect(await screen.findByText('এপিআই সচল')).toBeInTheDocument();
  });
});

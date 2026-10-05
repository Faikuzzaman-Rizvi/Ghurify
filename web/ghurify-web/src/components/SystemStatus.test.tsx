import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import { SystemStatus } from './SiteFooter';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import { stubApi } from '@/test/fetchStub';

/** The footer's live status light: React -> Vite proxy -> API -> SQL Server. */
describe('SystemStatus', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the healthy state once the API answers', async () => {
    stubApi([[/\/api\/v1\/health/, { status: 'Healthy', databaseStatus: 'Healthy' }]]);

    renderScreen(<SystemStatus />);

    expect(await screen.findByText('API healthy')).toBeInTheDocument();
    expect(screen.getByText(/Database/)).toBeInTheDocument();
  });

  it('shows a retry button when the API cannot be reached', async () => {
    stubApi([[/\/api\/v1\/health/, { title: 'Service unavailable', status: 503 }, 503]]);

    renderScreen(<SystemStatus />);

    expect(await screen.findByText('API not reachable')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('renders Bangla text when the language is Bangla', async () => {
    stubApi([[/\/api\/v1\/health/, { status: 'Healthy', databaseStatus: 'Healthy' }]]);
    await i18n.changeLanguage('bn');

    renderScreen(<SystemStatus />);

    expect(await screen.findByText('এপিআই সচল')).toBeInTheDocument();
  });
});

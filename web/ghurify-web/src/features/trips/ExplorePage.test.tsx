import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ExplorePage } from './ExplorePage';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import {
  requestedUrls,
  samplePage,
  sampleDestinations,
  sampleTrip,
  stubApi,
} from '@/test/fetchStub';

function requestedTripUrls(fetchMock: ReturnType<typeof stubApi>): string[] {
  return requestedUrls(fetchMock).filter((url) => url.startsWith('/api/v1/trips'));
}

describe('ExplorePage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('searches with the filters in the address, so a search can be shared', async () => {
    const fetchMock = stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/trips/, samplePage([sampleTrip])],
    ]);

    renderScreen(<ExplorePage />, { at: '/trips?destination=sajek&groupType=WomenOnly' });

    expect(await screen.findByRole('link', { name: 'Sajek sunrise weekend' })).toBeInTheDocument();
    expect(screen.getByText('1 trip')).toBeInTheDocument();
    expect(requestedTripUrls(fetchMock)).toContain(
      '/api/v1/trips?destination=sajek&groupType=WomenOnly&pageSize=12',
    );
  });

  it('ignores a malformed filter rather than sending it to the API', async () => {
    const fetchMock = stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/trips/, samplePage([sampleTrip])],
    ]);

    renderScreen(<ExplorePage />, { at: '/trips?groupType=Pirates&maxPrice=-5' });

    await screen.findByRole('link', { name: 'Sajek sunrise weekend' });
    expect(requestedTripUrls(fetchMock)).toEqual(['/api/v1/trips?pageSize=12']);
  });

  it('changing a filter runs a new search', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/trips/, samplePage([sampleTrip])],
    ]);

    renderScreen(<ExplorePage />, { at: '/trips' });
    await screen.findByRole('link', { name: 'Sajek sunrise weekend' });

    await user.selectOptions(screen.getByLabelText('Sort by'), 'PriceLowToHigh');

    await waitFor(() =>
      expect(requestedTripUrls(fetchMock)).toContain(
        '/api/v1/trips?sort=PriceLowToHigh&pageSize=12',
      ),
    );
  });

  it('offers to clear the filters when nothing matches', async () => {
    stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/trips/, samplePage([])],
    ]);

    renderScreen(<ExplorePage />, { at: '/trips?destination=sajek' });

    expect(await screen.findByText('No trips match these filters yet.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Clear filters' }).length).toBeGreaterThan(0);
  });
});

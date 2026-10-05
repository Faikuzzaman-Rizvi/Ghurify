import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import { HomePage } from './HomePage';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import {
  requestedUrls,
  samplePage,
  sampleDestinations,
  sampleTrip,
  stubApi,
} from '@/test/fetchStub';

function stubHomeApi() {
  return stubApi([
    [/\/api\/v1\/destinations$/, sampleDestinations],
    [/\/api\/v1\/trips/, samplePage([sampleTrip])],
  ]);
}

describe('HomePage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('leads with the search and the headline', () => {
    stubHomeApi();

    renderScreen(<HomePage />);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Find your people. Explore Bangladesh.' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Search trips' })).toBeInTheDocument();
    expect(screen.getByLabelText('Destination')).toBeInTheDocument();
  });

  it('shows destinations with their safety status and the trips leaving soon', async () => {
    stubHomeApi();

    renderScreen(<HomePage />);

    expect(await screen.findByRole('link', { name: 'Sajek Valley' })).toBeInTheDocument();
    expect(screen.getByText('Caution')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Sajek sunrise weekend' })).toBeInTheDocument();
    expect(screen.getByText('Tk 6,800', { selector: 'span' })).toBeInTheDocument();
  });

  it('asks only for the six soonest trips', async () => {
    const fetchMock = stubHomeApi();

    renderScreen(<HomePage />);
    await screen.findByRole('link', { name: 'Sajek sunrise weekend' });

    expect(requestedUrls(fetchMock)).toContain('/api/v1/trips?pageSize=6');
  });

  it('shows a retry when trips cannot be loaded', async () => {
    stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/trips/, { title: 'Service unavailable' }, 503],
    ]);

    renderScreen(<HomePage />);

    expect(
      await screen.findByText('We could not load trips. Check your connection and try again.'),
    ).toBeInTheDocument();
  });

  it('renders Bangla text and Bangla prices when the language is Bangla', async () => {
    stubHomeApi();
    await i18n.changeLanguage('bn');

    renderScreen(<HomePage />);

    expect(
      screen.getByRole('heading', { level: 1, name: 'সঙ্গী খুঁজুন। ঘুরে দেখুন বাংলাদেশ।' }),
    ).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'সাজেক ভ্যালি' })).toBeInTheDocument();
    expect(await screen.findByText('৳৬,৮০০', { selector: 'span' })).toBeInTheDocument();
  });
});

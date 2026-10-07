import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { TripDetailPage } from './TripDetailPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleTripDetail, stubApi } from '@/test/fetchStub';

function renderTrip(id: number | string = 7) {
  return renderScreen(<TripDetailPage />, { at: `/trips/${id}`, path: '/trips/:id' });
}

describe('TripDetailPage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({ status: 'anonymous', user: null });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows where every taka goes, adding up to the price', async () => {
    stubApi([[/\/api\/v1\/trips\/7$/, sampleTripDetail]]);

    renderTrip();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Sajek sunrise weekend' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Where your money goes')).toBeInTheDocument();
    expect(screen.getByText('Transport')).toBeInTheDocument();
    expect(screen.getByText('Safety buffer')).toBeInTheDocument();
    expect(screen.getByText('Total per person')).toBeInTheDocument();
  });

  it('shows the day-by-day plan with how hard each day is', async () => {
    stubApi([[/\/api\/v1\/trips\/7$/, sampleTripDetail]]);

    renderTrip();

    expect(await screen.findByText(/Sunrise over the clouds/)).toBeInTheDocument();
    expect(screen.getByText('Moderate')).toBeInTheDocument();
    expect(screen.getByText('5 of 14 left')).toBeInTheDocument();
  });

  it('asks a signed-out visitor to sign in before joining', async () => {
    stubApi([[/\/api\/v1\/trips\/7$/, sampleTripDetail]]);

    renderTrip();

    expect(await screen.findByRole('link', { name: 'Sign in to join' })).toHaveAttribute(
      'href',
      '/login',
    );
  });

  it('sends a join request with the note and says what happens next', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'r****i@example.com', displayName: null },
    });
    const fetchMock = stubApi([
      [/\/api\/v1\/trips\/7\/join-requests$/, { id: 31 }, 201],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderTrip();
    await user.click(await screen.findByRole('button', { name: 'Request to join' }));
    await user.type(screen.getByLabelText('A note for the host (optional)'), 'Two of us.');
    await user.click(screen.getByRole('button', { name: 'Send request' }));

    expect(await screen.findByText('Request sent.')).toBeInTheDocument();
    const sent = requests(fetchMock).find((request) => request.method === 'POST');
    expect(sent?.body).toBe('{"message":"Two of us."}');
  });

  it('points an unverified traveller to identity verification', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'r****i@example.com', displayName: null },
    });
    stubApi([
      [/\/api\/v1\/trips\/7\/join-requests$/, { title: 'Not allowed' }, 403],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderTrip();
    await user.click(await screen.findByRole('button', { name: 'Request to join' }));
    await user.click(screen.getByRole('button', { name: 'Send request' }));

    expect(
      await screen.findByText('Verify your national ID before asking to join.'),
    ).toBeInTheDocument();
  });

  it('shows the refund rules before anyone commits', async () => {
    stubApi([[/\/api\/v1\/trips\/7$/, sampleTripDetail]]);

    renderTrip();

    expect(await screen.findByText('If plans change')).toBeInTheDocument();
  });

  it('shows "not found" for a trip the API will not show, such as a draft', async () => {
    stubApi([[/\/api\/v1\/trips\/99$/, { title: 'Trip not found' }, 404]]);

    renderTrip(99);

    expect(
      await screen.findByText('This trip does not exist or is no longer available.'),
    ).toBeInTheDocument();
  });
});

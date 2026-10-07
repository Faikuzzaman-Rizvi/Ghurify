import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ReportDialog } from './ReportDialog';
import { TripSafetyPage } from './TripSafetyPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleTripDetail, stubApi } from '@/test/fetchStub';

const checkIn = {
  id: 21,
  tripId: 7,
  label: 'Reached the camp',
  dueAt: '2026-10-21T12:00:00Z',
  status: 'Scheduled',
  checkedInBy: null,
  checkedInOn: null,
  note: null,
};

const raised = {
  sosId: 3,
  emergencyContactTexted: true,
  nearestHelp: [
    {
      kind: 1,
      name: 'Sajek police camp',
      nameBn: 'সাজেক পুলিশ ক্যাম্প',
      phone: '+8801320000000',
      latitude: 23.38,
      longitude: 92.29,
      distanceMeters: 2400,
    },
  ],
};

function signInAs(id: number) {
  useAuthStore.setState({
    status: 'authenticated',
    user: { id, maskedEmail: 'm****u@example.com', displayName: 'Mitu' },
  });
}

function stubGeolocation(position: { latitude: number; longitude: number } | null) {
  vi.stubGlobal('navigator', {
    ...navigator,
    geolocation: {
      getCurrentPosition: (ok: PositionCallback, fail: PositionErrorCallback) =>
        position
          ? ok({ coords: { ...position, accuracy: 25 } } as GeolocationPosition)
          : fail({ code: 1, message: 'denied' } as GeolocationPositionError),
    },
  });
}

describe('Trip safety', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    signInAs(4);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends an SOS with the phone position and shows the nearest help', async () => {
    const user = userEvent.setup();
    stubGeolocation({ latitude: 23.381234, longitude: 92.291234 });
    const fetchMock = stubApi([
      [/\/api\/v1\/trips\/7\/sos$/, raised],
      [/\/api\/v1\/trips\/7\/check-ins$/, []],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<TripSafetyPage />, { at: '/trips/7/safety', path: '/trips/:id/safety' });

    await user.click(await screen.findByRole('button', { name: 'Send SOS' }));
    await user.type(screen.getByLabelText('What is happening? (optional)'), 'Lost on the trail');
    await user.click(screen.getByRole('button', { name: 'Send SOS now' }));

    expect(await screen.findByText('Sajek police camp')).toBeInTheDocument();
    expect(screen.getByText('Your emergency contact has been texted.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "I'm safe now" })).toBeInTheDocument();

    const sent = requests(fetchMock).find((request) => request.url.endsWith('/trips/7/sos'));
    expect(JSON.parse(sent!.body!)).toEqual({
      latitude: 23.381234,
      longitude: 92.291234,
      accuracyMeters: 25,
      message: 'Lost on the trail',
    });
  });

  it('says so when the position cannot be read, and still offers 999', async () => {
    const user = userEvent.setup();
    stubGeolocation(null);
    const fetchMock = stubApi([
      [/\/api\/v1\/trips\/7\/check-ins$/, []],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<TripSafetyPage />, { at: '/trips/7/safety', path: '/trips/:id/safety' });

    await user.click(await screen.findByRole('button', { name: 'Send SOS' }));
    await user.click(screen.getByRole('button', { name: 'Send SOS now' }));

    expect(await screen.findByText(/We could not get your location/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Call 999/ })).toHaveAttribute('href', 'tel:999');
    expect(requests(fetchMock).some((request) => request.url.endsWith('/sos'))).toBe(false);
  });

  it('a traveller marks a check-in safe, but cannot add one', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/check-ins\/21\/done$/, null, 204],
      [/\/api\/v1\/trips\/7\/check-ins$/, [checkIn]],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<TripSafetyPage />, { at: '/trips/7/safety', path: '/trips/:id/safety' });

    await user.click(await screen.findByRole('button', { name: "We're safe" }));
    await waitFor(() =>
      expect(
        requests(fetchMock).some((request) => request.url.endsWith('/check-ins/21/done')),
      ).toBe(true),
    );
    expect(screen.queryByRole('button', { name: 'Add check-in' })).not.toBeInTheDocument();
  });

  it('the host can add a check-in', async () => {
    signInAs(2);
    stubApi([
      [/\/api\/v1\/trips\/7\/check-ins$/, []],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<TripSafetyPage />, { at: '/trips/7/safety', path: '/trips/:id/safety' });

    expect(await screen.findByRole('button', { name: 'Add check-in' })).toBeDisabled();
    expect(screen.getByText(/Add one for each risky stretch/)).toBeInTheDocument();
  });

  it('a report is sent with its reason and details', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([[/\/api\/v1\/reports$/, { id: 1 }, 201]]);

    renderScreen(<ReportDialog kind="User" targetId={30} open onClose={() => undefined} />);

    await user.selectOptions(screen.getByLabelText('Reason'), 'Fraud');
    await user.type(screen.getByLabelText('Details'), 'Asked for money outside Ghurify.');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/Our moderators will look into it/)).toBeInTheDocument();
    const sent = requests(fetchMock).find((request) => request.url.endsWith('/api/v1/reports'));
    expect(JSON.parse(sent!.body!)).toEqual({
      kind: 'User',
      targetId: 30,
      reason: 'Fraud',
      details: 'Asked for money outside Ghurify.',
    });
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ManageRequestsPage } from './ManageRequestsPage';
import { MyTripsPage } from './MyTripsPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleTripDetail, stubApi } from '@/test/fetchStub';

const pending = {
  id: 31,
  userId: 4,
  displayName: 'Nusrat Jahan',
  gender: 'Female',
  verifiedLevel: 'Nid',
  message: 'Two of us, first time in the hills.',
  status: 'Pending',
  created: '2026-10-05T06:00:00Z',
  bookingId: null,
  bookingStatus: null,
  holdExpiresAt: null,
};

const heldBooking = {
  requestId: 31,
  requestStatus: 'Approved',
  requestedOn: '2026-10-05T06:00:00Z',
  tripId: 7,
  title: 'Sajek sunrise weekend',
  destinationSlug: 'sajek',
  destinationName: 'Sajek Valley',
  destinationNameBn: 'সাজেক ভ্যালি',
  destinationKind: 'Hills',
  destinationStatus: 'Open',
  startDate: '2026-10-14',
  endDate: '2026-10-16',
  tripStatus: 'Published',
  hostName: 'Tanvir Hasan',
  bookingId: 90,
  bookingStatus: 'Held',
  amount: 6800,
  holdExpiresAt: new Date(Date.now() + 20 * 60_000).toISOString(),
};

describe('booking screens', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 2, maskedEmail: 't****r@example.com', displayName: 'Tanvir Hasan' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lets the host approve a pending request', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [
        /\/api\/v1\/join-requests\/31\/approve$/,
        { bookingId: 90, holdExpiresAt: heldBooking.holdExpiresAt },
      ],
      [/\/api\/v1\/trips\/7\/join-requests$/, [pending]],
      [/\/api\/v1\/trips\/7$/, sampleTripDetail],
    ]);

    renderScreen(<ManageRequestsPage />, {
      at: '/host/trips/7/requests',
      path: '/host/trips/:id/requests',
    });

    expect(await screen.findByText('Two of us, first time in the hills.')).toBeInTheDocument();
    expect(screen.getByText('ID verified')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Approve and hold a seat' }));

    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) =>
            request.method === 'POST' && request.url.endsWith('/join-requests/31/approve'),
        ),
      ).toBe(true),
    );
  });

  it('shows the traveller a held seat with the time left to pay', async () => {
    stubApi([[/\/api\/v1\/me\/bookings$/, [heldBooking]]]);

    renderScreen(<MyTripsPage />);

    expect(await screen.findByText('Sajek sunrise weekend')).toBeInTheDocument();
    expect(screen.getByText(/^Pay within \d+:\d\d$/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pay Tk 6,800' })).toHaveAttribute(
      'href',
      '/bookings/90/checkout',
    );
  });

  it('invites someone with no trips to explore', async () => {
    stubApi([[/\/api\/v1\/me\/bookings$/, []]]);

    renderScreen(<MyTripsPage />);

    expect(await screen.findByText('You have not asked to join a trip yet.')).toBeInTheDocument();
  });
});

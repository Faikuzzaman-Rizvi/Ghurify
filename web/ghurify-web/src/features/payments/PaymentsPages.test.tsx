import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CheckoutPage } from './CheckoutPage';
import { PaymentResultPage } from './PaymentResultPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

const checkout = {
  bookingId: 90,
  tripId: 7,
  tripTitle: 'Sajek sunrise weekend',
  startDate: '2026-10-14',
  endDate: '2026-10-16',
  hostName: 'Tanvir Hasan',
  amount: 6800,
  fee: 136,
  total: 6936,
  status: 'Held',
  holdExpiresAt: new Date(Date.now() + 20 * 60_000).toISOString(),
  latestPaymentStatus: null,
  latestPaymentFailure: null,
};

describe('payment screens', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'n****t@example.com', displayName: 'Nusrat' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the fee before paying, and starts a payment with an idempotency key and no amount', async () => {
    const user = userEvent.setup();
    const assign = vi.fn();
    vi.stubGlobal('location', { ...window.location, assign });
    const fetchMock = stubApi([
      [
        /\/api\/v1\/bookings\/90\/payments$/,
        {
          paymentId: 5,
          redirectUrl: 'https://gateway.test/pay',
          amount: 6800,
          fee: 136,
          total: 6936,
        },
      ],
      [/\/api\/v1\/bookings\/90\/checkout$/, checkout],
    ]);

    renderScreen(<CheckoutPage />, { at: '/bookings/90/checkout', path: '/bookings/:id/checkout' });

    expect(await screen.findByText('Service fee')).toBeInTheDocument();
    expect(screen.getByText('Tk 136')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Pay Tk 6,936' }));

    await waitFor(() => expect(assign).toHaveBeenCalledWith('https://gateway.test/pay'));
    const start = requests(fetchMock).find((request) => request.method === 'POST');
    expect(start?.body).toBeUndefined();
    const init = fetchMock.mock.calls.find(([, options]) => options?.method === 'POST')?.[1];
    expect((init?.headers as Record<string, string>)['Idempotency-Key']).toMatch(/^[a-f0-9]{32}$/);
  });

  it('says plainly when a seat is already paid for', async () => {
    stubApi([[/\/api\/v1\/bookings\/90\/checkout$/, { ...checkout, status: 'Confirmed' }]]);

    renderScreen(<CheckoutPage />, { at: '/bookings/90/checkout', path: '/bookings/:id/checkout' });

    expect(await screen.findByText('This seat is already paid for.')).toBeInTheDocument();
  });

  it('only celebrates once the API confirms the booking, whatever the URL says', async () => {
    stubApi([
      [
        /\/api\/v1\/bookings\/90\/checkout$/,
        { ...checkout, status: 'Held', latestPaymentStatus: 'Pending' },
      ],
    ]);

    renderScreen(<PaymentResultPage />, { at: '/payments/result?booking=90&outcome=success' });

    expect(await screen.findByText('Confirming your payment')).toBeInTheDocument();
    expect(screen.queryByText('You are going!')).not.toBeInTheDocument();
  });

  it('shows the confirmation once the booking is paid', async () => {
    stubApi([
      [
        /\/api\/v1\/bookings\/90\/checkout$/,
        { ...checkout, status: 'Confirmed', latestPaymentStatus: 'Succeeded' },
      ],
    ]);

    renderScreen(<PaymentResultPage />, { at: '/payments/result?booking=90&outcome=success' });

    expect(await screen.findByText('You are going!')).toBeInTheDocument();
  });
});

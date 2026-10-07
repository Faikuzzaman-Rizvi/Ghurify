import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PaymentHistoryPage } from './PaymentHistoryPage';
import { PaymentReceiptPage } from './PaymentReceiptPage';
import { ReceivedPaymentsPage } from './ReceivedPaymentsPage';
import { SandboxPaymentPage } from './SandboxPaymentPage';
import { AdminPaymentDetailPage } from '@/features/admin/AdminPaymentDetailPage';
import { AdminPaymentsPage } from '@/features/admin/AdminPaymentsPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requestedUrls, requests, stubApi } from '@/test/fetchStub';

const paid = {
  id: 41,
  bookingId: 90,
  tripId: 7,
  tripTitle: 'Sajek sunrise weekend',
  travellerId: 1,
  travellerName: 'Nusrat',
  provider: 'sslcommerz',
  transactionRef: 'GHRABC123',
  providerTxnId: 'BT999',
  status: 'Succeeded',
  methodType: 'MobileBanking',
  methodName: 'bKash',
  accountLast4: '7788',
  amount: 6800,
  fee: 136,
  total: 6936,
  paidAmount: 6936,
  currency: 'BDT',
  refunded: 3400,
  created: '2026-10-07T09:30:00Z',
  completedOn: '2026-10-07T09:31:00Z',
};

const declined = {
  ...paid,
  id: 40,
  transactionRef: 'GHRFAIL1',
  providerTxnId: null,
  status: 'Failed',
  methodType: null,
  methodName: null,
  accountLast4: null,
  paidAmount: null,
  refunded: 0,
  completedOn: null,
};

const history = {
  items: [paid, declined],
  totals: { count: 2, succeeded: 1, paid: 6936, refunded: 3400, net: 3536 },
  totalCount: 2,
  page: 1,
  pageSize: 20,
};

const receipt = {
  id: 41,
  bookingId: 90,
  bookingStatus: 'Confirmed',
  tripId: 7,
  tripTitle: 'Sajek sunrise weekend',
  startDate: '2026-10-14',
  endDate: '2026-10-16',
  hostName: 'Tanvir Hasan',
  provider: 'sslcommerz',
  transactionRef: 'GHRABC123',
  providerTxnId: 'BT999',
  validationId: 'VAL123',
  status: 'Succeeded',
  failureReason: null,
  methodType: 'MobileBanking',
  methodName: 'bKash',
  accountLast4: '7788',
  issuer: 'bKash Limited',
  amount: 6800,
  fee: 136,
  total: 6936,
  paidAmount: 6936,
  currency: 'BDT',
  refunded: 3400,
  created: '2026-10-07T09:30:00Z',
  completedOn: '2026-10-07T09:31:00Z',
  gatewayPaidOn: '2026-10-07T09:30:30Z',
  refunds: [
    {
      id: 5,
      amount: 3400,
      shortfall: 0,
      reason: 'TravelerCancelled',
      status: 'Succeeded',
      providerRefundRef: 'RF-778',
      failureReason: null,
      created: '2026-10-08T04:00:00Z',
      completedOn: '2026-10-08T04:05:00Z',
    },
  ],
  netPaid: 3536,
};

describe('payment history', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'n****t@example.com', displayName: 'Nusrat' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('lists every attempt with how it was paid, its transaction ID, its time in Dhaka and the totals', async () => {
    stubApi([[/\/api\/v1\/me\/payments\?/, history]]);

    renderScreen(<PaymentHistoryPage />, { at: '/me/payments' });

    expect(await screen.findByText('GHRABC123')).toBeInTheDocument();
    expect(screen.getByText('GHRFAIL1')).toBeInTheDocument();
    expect(screen.getByText('bKash')).toBeInTheDocument();
    expect(screen.getByText('7788')).toBeInTheDocument();
    expect(screen.getByText('Not paid')).toBeInTheDocument();
    expect(screen.getByText('Tk 3,400 refunded')).toBeInTheDocument();
    // 09:30 UTC is 15:30 in Dhaka.
    expect(screen.getAllByText(/15:30/).length).toBeGreaterThan(0);
    // Paid, refunded and net, over every payment.
    expect(screen.getAllByText('Tk 6,936').length).toBeGreaterThan(1);
    expect(screen.getByText('Tk 3,536')).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'View receipt' })[0]).toHaveAttribute(
      'href',
      '/me/payments/41',
    );
  });

  it('asks the API for one status when a filter is chosen', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([[/\/api\/v1\/me\/payments\?/, history]]);
    renderScreen(<PaymentHistoryPage />, { at: '/me/payments' });
    await screen.findByText('GHRABC123');

    const filters = screen.getByRole('group', { name: 'Show payments' });
    await user.click(within(filters).getByRole('button', { name: 'Failed' }));

    await waitFor(() =>
      expect(requestedUrls(fetchMock).some((url) => url.includes('status=Failed'))).toBe(true),
    );
    expect(within(filters).getByRole('button', { name: 'Failed' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
  });

  it('copies a transaction ID to quote to support', async () => {
    const user = userEvent.setup();
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    stubApi([[/\/api\/v1\/me\/payments\?/, history]]);
    renderScreen(<PaymentHistoryPage />, { at: '/me/payments' });
    await screen.findByText('GHRABC123');

    await user.click(screen.getAllByRole('button', { name: 'Copy Transaction ID' })[0]!);

    expect(writeText).toHaveBeenCalledWith('GHRABC123');
    expect(await screen.findByText('Copied')).toBeInTheDocument();
  });

  it('says so, with a way forward, when nothing has been paid yet', async () => {
    stubApi([
      [
        /\/api\/v1\/me\/payments\?/,
        { ...history, items: [], totalCount: 0, totals: { ...history.totals, count: 0 } },
      ],
    ]);
    renderScreen(<PaymentHistoryPage />, { at: '/me/payments' });

    expect(await screen.findByText('No payments yet')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Find a trip' })).toHaveAttribute('href', '/trips');
  });

  it('shows a receipt with every reference, amount, date and refund, and prints it', async () => {
    const user = userEvent.setup();
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    stubApi([[/\/api\/v1\/me\/payments\/41$/, receipt]]);

    renderScreen(<PaymentReceiptPage />, { at: '/me/payments/41', path: '/me/payments/:id' });

    expect(await screen.findByText('Payment #41')).toBeInTheDocument();
    expect(screen.getAllByText('GHRABC123').length).toBeGreaterThan(0);
    expect(screen.getByText('BT999')).toBeInTheDocument();
    expect(screen.getByText('VAL123')).toBeInTheDocument();
    expect(screen.getByText('SSLCommerz')).toBeInTheDocument();
    expect(screen.getByText('bKash Limited')).toBeInTheDocument();
    expect(screen.getByText('Mobile banking')).toBeInTheDocument();
    expect(screen.getByText('Tk 6,800')).toBeInTheDocument();
    expect(screen.getByText('Tk 136')).toBeInTheDocument();
    expect(screen.getByText('Tk 3,536')).toBeInTheDocument();
    expect(screen.getByText('Booking #90 · Paid')).toBeInTheDocument();
    expect(screen.getByText(/You cancelled/)).toBeInTheDocument();
    expect(screen.getByText(/RF-778/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sajek sunrise weekend' })).toHaveAttribute(
      'href',
      '/trips/7',
    );

    await user.click(screen.getByRole('button', { name: 'Print receipt' }));
    expect(print).toHaveBeenCalled();
  });

  it('explains a failed attempt on its receipt', async () => {
    stubApi([
      [
        /\/api\/v1\/me\/payments\/40$/,
        {
          ...receipt,
          id: 40,
          status: 'Failed',
          failureReason: 'cancelled_by_traveler',
          methodType: null,
          methodName: null,
          accountLast4: null,
          issuer: null,
          paidAmount: null,
          refunded: 0,
          refunds: [],
          netPaid: 0,
        },
      ],
    ]);

    renderScreen(<PaymentReceiptPage />, { at: '/me/payments/40', path: '/me/payments/:id' });

    expect(await screen.findByText('You cancelled on the payment page.')).toBeInTheDocument();
    expect(screen.getByText('Amount to pay')).toBeInTheDocument();
    expect(screen.getByText('Nothing has been refunded for this payment.')).toBeInTheDocument();
  });

  it("says someone else's payment does not exist", async () => {
    stubApi([
      [/\/api\/v1\/me\/payments\/99$/, { title: 'Not found', code: 'payment_not_found' }, 404],
    ]);

    renderScreen(<PaymentReceiptPage />, { at: '/me/payments/99', path: '/me/payments/:id' });

    expect(await screen.findByText('There is no such payment.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Print receipt' })).not.toBeInTheDocument();
  });

  it('shows a host the seats paid for on one trip, without how anyone paid', async () => {
    const fetchMock = stubApi([
      [
        /\/api\/v1\/me\/received-payments\?/,
        {
          items: [
            {
              id: 41,
              bookingId: 90,
              tripId: 7,
              tripTitle: 'Sajek sunrise weekend',
              travellerId: 1,
              travellerName: 'Nusrat',
              transactionRef: 'GHRABC123',
              amount: 6800,
              refunded: 0,
              currency: 'BDT',
              paidOn: '2026-10-07T09:31:00Z',
            },
          ],
          totals: { count: 1, bookingValue: 6800, refunded: 0 },
          totalCount: 1,
          page: 1,
          pageSize: 20,
        },
      ],
    ]);

    renderScreen(<ReceivedPaymentsPage />, { at: '/host/payments?trip=7' });

    expect(await screen.findByRole('link', { name: 'Nusrat' })).toHaveAttribute('href', '/users/1');
    expect(screen.getAllByText('Tk 6,800').length).toBeGreaterThan(0);
    expect(screen.queryByText('bKash')).not.toBeInTheDocument();
    expect(requestedUrls(fetchMock).some((url) => url.includes('tripId=7'))).toBe(true);
    expect(screen.getByRole('button', { name: /Show all trips/ })).toBeInTheDocument();
  });

  it('sends the method chosen on the sandbox page', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/payments\/sandbox\/GHR1\/complete$/, { bookingId: 90, outcome: 'confirmed' }],
      [
        /\/api\/v1\/payments\/sandbox\/GHR1$/,
        { reference: 'GHR1', bookingId: 90, total: 6936, status: 'Pending' },
      ],
    ]);
    renderScreen(<SandboxPaymentPage />, { at: '/payments/sandbox?ref=GHR1' });

    await user.click(await screen.findByRole('radio', { name: 'Nagad' }));
    await user.click(screen.getByRole('button', { name: 'Pay successfully' }));

    await waitFor(() => {
      const complete = requests(fetchMock).find((request) => request.method === 'POST');
      expect(JSON.parse(complete?.body ?? '{}')).toEqual({ succeed: true, method: 'nagad' });
    });
  });

  it('lets the admin desk search payments and open one with its settlement and callbacks', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([[/\/api\/v1\/admin\/payments\?/, history]]);
    renderScreen(<AdminPaymentsPage />, { at: '/admin/payments' });
    await screen.findByText('GHRFAIL1');

    await user.type(screen.getByRole('searchbox'), 'GHRABC123');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() =>
      expect(requestedUrls(fetchMock).some((url) => url.includes('search=GHRABC123'))).toBe(true),
    );
    expect(screen.getByRole('link', { name: /#41/ })).toHaveAttribute('href', '/admin/payments/41');
  });

  it('shows the admin desk who paid, what the gateway kept and every callback', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/payments\/41$/,
        {
          payment: receipt,
          travellerId: 1,
          travellerName: 'Nusrat',
          travellerEmail: 'nusrat@example.com',
          hostId: 2,
          storeAmount: 6797.28,
          riskFlagged: false,
          callbacks: [
            {
              id: 3,
              eventId: 'VAL123',
              signatureValid: true,
              outcome: 'confirmed',
              received: '2026-10-07T09:31:00Z',
              processedOn: '2026-10-07T09:31:01Z',
            },
          ],
        },
      ],
    ]);

    renderScreen(<AdminPaymentDetailPage />, {
      at: '/admin/payments/41',
      path: '/admin/payments/:id',
    });

    expect(await screen.findByText('nusrat@example.com')).toBeInTheDocument();
    expect(screen.getByText('Tk 6,797')).toBeInTheDocument();
    expect(screen.getByText('Tk 139')).toBeInTheDocument();
    expect(screen.getByText('Clear')).toBeInTheDocument();
    expect(screen.getByText(/Signature valid · confirmed/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nusrat' })).toHaveAttribute('href', '/admin/users/1');
  });
});

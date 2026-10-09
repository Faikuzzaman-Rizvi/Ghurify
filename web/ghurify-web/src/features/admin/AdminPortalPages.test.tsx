import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AdminTripsPage } from './AdminTripsPage';
import { AuditLogPage } from './AuditLogPage';
import { BookingLookupPage } from './BookingLookupPage';
import { EmergencyPointsPage } from './EmergencyPointsPage';
import { UserDetailPage } from './UserDetailPage';
import { UsersPage } from './UsersPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleDestinations, stubApi } from '@/test/fetchStub';

const person = {
  id: 30,
  email: 'mitu@example.com',
  displayName: 'Mitu Akter',
  phone: '+8801711000111',
  gender: 'Female',
  status: 'Active',
  created: '2026-03-01T06:00:00Z',
  homeDistrict: 'Dhaka',
  emergencyContactName: null,
  emergencyContactPhone: null,
  avatarVersion: null,
  hasPassword: true,
  mustResetPassword: false,
  roles: ['Traveler', 'Host'],
  verifications: [
    {
      id: 5,
      level: 'NidSelfie',
      status: 'Approved',
      idType: 'Nid',
      provider: 'manual',
      reason: null,
      created: '2026-03-02T06:00:00Z',
      reviewedOn: '2026-03-03T06:00:00Z',
      documentCount: 3,
    },
  ],
  hostedTrips: [
    {
      id: 7,
      title: 'Sajek sunrise weekend',
      status: 'Published',
      startDate: '2026-10-20',
      seats: 10,
      seatsTaken: 4,
    },
  ],
  bookings: [],
  openReports: 1,
  totalReports: 2,
};

describe('Admin portal pages', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'a****n@example.com', displayName: 'Admin' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('finds people by email and links to their record', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [
        /\/api\/v1\/admin\/users\?/,
        {
          items: [
            {
              id: 30,
              email: 'mitu@example.com',
              displayName: 'Mitu Akter',
              phone: null,
              status: 'Suspended',
              created: '2026-03-01T06:00:00Z',
              verifiedLevel: 'Nid',
              roles: ['Traveler'],
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 25,
        },
      ],
    ]);

    renderScreen(<UsersPage />);
    await user.type(screen.getByLabelText('Search'), 'mitu@');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    expect(await screen.findByRole('link', { name: /Mitu Akter/ })).toHaveAttribute(
      'href',
      '/admin/users/30',
    );
    expect(screen.getByRole('link', { name: /Mitu Akter/ })).toHaveTextContent('Suspended');
    await waitFor(() =>
      expect(requests(fetchMock).some((request) => request.url.includes('search=mitu%40'))).toBe(
        true,
      ),
    );
  });

  it('suspends someone only with a written reason', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/users\/30\/status$/, null, 204],
      [/\/api\/v1\/admin\/users\/30$/, person],
    ]);

    renderScreen(<UserDetailPage />, { at: '/admin/users/30', path: '/admin/users/:id' });
    await user.click(await screen.findByRole('button', { name: 'Suspend' }));

    const confirm = screen.getAllByRole('button', { name: 'Suspend' }).at(-1)!;
    expect(confirm).toBeDisabled();
    await user.type(screen.getByLabelText('Reason'), 'Repeated harassment reports.');
    await user.click(confirm);

    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) =>
            request.url.endsWith('/admin/users/30/status') &&
            request.body === '{"status":"Suspended","reason":"Repeated harassment reports."}',
        ),
      ).toBe(true),
    );
    expect(screen.getByText(/1 open, 2 in total/)).toBeInTheDocument();
  });

  it('cancels a trip with a reason', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/trips\/7\/cancel$/, null, 204],
      [
        /\/api\/v1\/admin\/trips\?/,
        {
          items: [
            {
              id: 7,
              title: 'Sajek sunrise weekend',
              hostId: 30,
              hostName: 'Mitu Akter',
              destinationName: 'Sajek Valley',
              startDate: '2026-10-20',
              endDate: '2026-10-22',
              status: 'Published',
              seats: 10,
              seatsTaken: 4,
              pricePerPerson: 6000,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 25,
        },
      ],
    ]);

    renderScreen(<AdminTripsPage />);
    await user.click(await screen.findByRole('button', { name: 'Cancel trip' }));
    await user.type(screen.getByLabelText('Reason'), 'Host unreachable.');
    await user.click(screen.getAllByRole('button', { name: 'Cancel trip' }).at(-1)!);

    await waitFor(() =>
      expect(
        requests(fetchMock).some((request) => request.url.endsWith('/admin/trips/7/cancel')),
      ).toBe(true),
    );
  });

  it('shows where a booking’s money is', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/bookings\/lookup\?q=SANDBOX-1$/,
        {
          id: 12,
          tripId: 7,
          tripTitle: 'Sajek sunrise weekend',
          startDate: '2026-10-20',
          hostId: 30,
          hostName: 'Mitu Akter',
          travellerId: 40,
          travellerName: 'Rafi',
          status: 'Refunded',
          amount: 6000,
          created: '2026-10-01T06:00:00Z',
          confirmedOn: '2026-10-01T06:10:00Z',
          cancelledOn: '2026-10-05T06:00:00Z',
          payments: [
            {
              id: 1,
              provider: 'fake',
              transactionRef: 'SANDBOX-1',
              status: 'Succeeded',
              amount: 6000,
              fee: 120,
              total: 6120,
              paidAmount: 6120,
              failureReason: null,
              created: '2026-10-01T06:05:00Z',
              completedOn: '2026-10-01T06:10:00Z',
            },
          ],
          refunds: [
            {
              id: 2,
              amount: 6120,
              reason: 'Admin',
              status: 'Failed',
              reference: 'refund:1',
              failureReason: 'Gateway timeout',
              attempts: 2,
              created: '2026-10-05T06:00:00Z',
              completedOn: null,
            },
          ],
          held: 6120,
          released: 0,
          refunded: 0,
          inEscrow: 6120,
        },
      ],
    ]);

    renderScreen(<BookingLookupPage />, {
      at: '/admin/bookings?q=SANDBOX-1',
      path: '/admin/bookings',
    });

    expect(await screen.findByText('Booking #12 · Refunded')).toBeInTheDocument();
    expect(screen.getByText('Still in escrow').nextSibling).toHaveTextContent('Tk 6,120');
    expect(screen.getByText(/Gateway timeout/)).toBeInTheDocument();
  });

  it('lets the desk correct an emergency point and mark it checked', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [
        /\/api\/v1\/admin\/emergency-points$/,
        [
          {
            id: 3,
            destinationSlug: 'sajek',
            destinationName: 'Sajek Valley',
            kind: 1,
            name: 'Sajek police camp',
            nameBn: 'সাজেক পুলিশ ক্যাম্প',
            phone: null,
            latitude: 23.38,
            longitude: 92.29,
            checkedOn: null,
            checkedBy: null,
          },
        ],
      ],
    ]);

    renderScreen(<EmergencyPointsPage />);
    expect(await screen.findByText('1 not checked yet')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Edit' }));
    await user.type(screen.getByLabelText('Phone number'), '01320-000000');
    await user.click(screen.getByLabelText(/I have confirmed/));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      const saved = requests(fetchMock).find((request) => request.method === 'POST');
      expect(JSON.parse(saved!.body!)).toMatchObject({
        id: 3,
        destinationSlug: 'sajek',
        phone: '01320-000000',
        checked: true,
      });
    });
  });

  it('lists audit entries with who did what, and what changed', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/audit/,
        {
          total: 2,
          page: 1,
          pageSize: 50,
          entries: [
            {
              id: 1,
              actorId: 1,
              actorName: 'Admin',
              action: 'user.suspended',
              entityType: 'User',
              entityId: 30,
              note: 'Harassment.',
              changes: [],
              created: '2026-10-06T06:00:00Z',
            },
            {
              id: 2,
              actorId: 1,
              actorName: 'Admin',
              action: 'staff.role.update',
              entityType: 'StaffRole',
              entityId: 4,
              note: 'payments-desk',
              changes: [
                { field: 'permissions', from: 'payments.view', to: 'payments.view, payouts.view' },
              ],
              created: '2026-10-06T07:00:00Z',
            },
          ],
        },
      ],
    ]);

    renderScreen(<AuditLogPage />);

    expect(await screen.findByText('user.suspended')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '30' })).toHaveAttribute('href', '/admin/users/30');
    expect(screen.getByText(/Harassment\./)).toBeInTheDocument();

    // The before/after pair, so "who changed what" is answerable from the list itself.
    expect(screen.getByText('permissions')).toBeInTheDocument();
    expect(screen.getByText('payments.view')).toBeInTheDocument();
    expect(screen.getByText('payments.view, payouts.view')).toBeInTheDocument();
  });
});

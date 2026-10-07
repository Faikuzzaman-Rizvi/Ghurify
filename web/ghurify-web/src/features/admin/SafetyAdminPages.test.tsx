import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AdminDashboardPage } from './AdminDashboardPage';
import { DestinationAlertsPage } from './DestinationAlertsPage';
import { PayoutQueuePage } from './PayoutQueuePage';
import { ReportsQueuePage } from './ReportsQueuePage';
import { SosBoardPage } from './SosBoardPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, sampleDestinations, stubApi } from '@/test/fetchStub';

function profileWith(roles: string[]) {
  return {
    userId: 9,
    maskedEmail: 'd****k@example.com',
    displayName: 'Desk',
    gender: 'Female',
    phone: null,
    bio: null,
    homeDistrict: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
    roles,
    verifiedLevel: null,
    memberSince: '2026-01-10',
  };
}

const counts = {
  pendingVerifications: 3,
  openReports: 2,
  openDisputes: 1,
  openSos: 1,
  missedCheckIns: 0,
  payoutsAwaitingApproval: 4,
  refundsInFlight: 0,
  closedDestinations: 0,
  cautionDestinations: 1,
  liveTrips: 12,
  bookingsConfirmed: 5,
};

const report = (id: number, kind: string, reason = 'Harassment') => ({
  id,
  reporterId: 4,
  reporterName: 'Mitu',
  kind,
  targetId: 30,
  reason,
  details: 'Rude messages in the group chat.',
  status: 'Open',
  resolution: null,
  created: '2026-10-05T06:00:00Z',
});

describe('Admin safety pages', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 9, maskedEmail: 'd****k@example.com', displayName: 'Desk' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('dashboard links only to the queues the viewer can open', async () => {
    stubApi([
      [/\/api\/v1\/admin\/dashboard$/, counts],
      [/\/api\/v1\/me\/profile$/, profileWith(['Moderator'])],
    ]);

    renderScreen(<AdminDashboardPage />);

    const reports = await screen.findByText('Open reports');
    await waitFor(() => expect(reports.closest('a')).toHaveAttribute('href', '/admin/reports'));
    expect(screen.getByText('Open SOS alerts').closest('a')).toBeNull();
    expect(screen.getByText('Live trips')).toBeInTheDocument();
  });

  it('reports need a written reason, and offer actions that fit the report', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/reports\/5\/resolve$/, null, 204],
      [/\/api\/v1\/admin\/reports$/, [report(5, 'User'), report(6, 'Dispute', 'Payment')]],
    ]);

    renderScreen(<ReportsQueuePage />);

    const suspend = await screen.findByRole('button', { name: 'Suspend the person' });
    expect(suspend).toBeDisabled();
    // The dispute belongs on the disputes page, not here.
    expect(screen.queryByRole('button', { name: 'Refund in full' })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText(/What you decided/), 'Warned; no evidence of threats.');
    await user.click(screen.getByRole('button', { name: 'Dismiss' }));

    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) =>
            request.url.endsWith('/admin/reports/5/resolve') &&
            request.body === '{"action":"Dismiss","resolution":"Warned; no evidence of threats."}',
        ),
      ).toBe(true),
    );
  });

  it('disputes can be refunded', async () => {
    stubApi([[/\/api\/v1\/admin\/reports\?kind=Dispute$/, [report(6, 'Dispute', 'Payment')]]]);

    renderScreen(<ReportsQueuePage disputes />);

    expect(await screen.findByRole('button', { name: 'Refund in full' })).toBeInTheDocument();
    expect(screen.getByText('Booking #30')).toBeInTheDocument();
  });

  it('closing a destination needs both notes and an explicit confirmation', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/destinations\/sajek\/status$/, null, 204],
      [/\/api\/v1\/destinations$/, sampleDestinations],
    ]);

    renderScreen(<DestinationAlertsPage />);

    const change = await screen.findAllByRole('button', { name: 'Change status' });
    await user.click(change[0]!);
    await user.click(screen.getByLabelText('Closed'));

    const save = screen.getByRole('button', { name: 'Save' });
    expect(save).toBeDisabled();

    await user.type(screen.getByLabelText('Note in English'), 'Landslides on the road.');
    await user.type(screen.getByLabelText('Note in Bangla'), 'রাস্তায় ভূমিধস।');
    expect(save).toBeDisabled();

    await user.click(screen.getByRole('checkbox'));
    await user.click(save);

    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) =>
            request.url.endsWith('/admin/destinations/sajek/status') &&
            request.body ===
              '{"status":"Closed","note":"Landslides on the road.","noteBn":"রাস্তায় ভূমিধস।"}',
        ),
      ).toBe(true),
    );
  });

  it('the SOS board lists open alerts and acknowledges one', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/sos\/3\/acknowledge$/, null, 204],
      [
        /\/api\/v1\/admin\/sos\?includeResolved=false$/,
        [
          {
            id: 3,
            userId: 4,
            userName: 'Mitu',
            tripId: 7,
            tripTitle: 'Sajek sunrise weekend',
            latitude: 23.38,
            longitude: 92.29,
            message: 'Lost on the trail',
            status: 'Open',
            created: '2026-10-05T06:00:00Z',
            lastSeenOn: '2026-10-05T06:05:00Z',
            userPhone: '+8801711000111',
            hostName: 'Tanvir',
            hostPhone: '+8801811000222',
          },
        ],
      ],
      [/\/api\/v1\/admin\/check-ins\/missed$/, []],
    ]);

    renderScreen(<SosBoardPage />);

    expect(await screen.findByText('“Lost on the trail”')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '+8801711000111' })).toHaveAttribute(
      'href',
      'tel:+8801711000111',
    );
    expect(screen.getByText('No missed check-ins today.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Acknowledge' }));
    await waitFor(() =>
      expect(
        requests(fetchMock).some((request) => request.url.endsWith('/admin/sos/3/acknowledge')),
      ).toBe(true),
    );
  });

  it('payouts waiting to be sent can be marked sent', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/payouts\/8\/approve$/, null, 204],
      [
        /\/api\/v1\/admin\/payouts\?status=Released$/,
        [
          {
            id: 8,
            tripId: 7,
            tripTitle: 'Sajek sunrise weekend',
            startDate: '2026-10-20',
            hostId: 2,
            hostName: 'Tanvir Hasan',
            stage: 'BeforeDeparture',
            amount: 9600,
            platformAmount: 0,
            status: 'Released',
            created: '2026-10-17T00:00:00Z',
            approvedOn: null,
          },
        ],
      ],
    ]);

    renderScreen(<PayoutQueuePage />);

    expect(await screen.findByText('Tk 9,600')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Mark as sent' }));

    await waitFor(() =>
      expect(
        requests(fetchMock).some((request) => request.url.endsWith('/admin/payouts/8/approve')),
      ).toBe(true),
    );
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { TripWizardPage } from './TripWizardPage';
import { HostTripsPage } from './HostTripsPage';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { chooseOption } from '@/test/chooseOption';
import { requests, sampleDestinations, stubApi } from '@/test/fetchStub';

function hostProfile(verifiedLevel: string | null) {
  return {
    userId: 2,
    maskedEmail: 't****r@example.com',
    displayName: 'Tanvir Hasan',
    gender: 'Male',
    phone: null,
    bio: null,
    homeDistrict: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
    roles: ['Traveler', 'Host'],
    verifiedLevel,
    memberSince: '2026-01-10',
  };
}

/** A date `days` from today, as the date input wants it. */
function inDays(days: number): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

describe('TripWizardPage', () => {
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

  it('will not leave the basics step until the required fields are filled', async () => {
    const user = userEvent.setup();
    stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/me\/profile$/, hostProfile('NidSelfie')],
    ]);

    renderScreen(<TripWizardPage />);
    await user.click(screen.getByRole('button', { name: 'Next →' }));

    expect(await screen.findByText('Choose a destination.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '1. Basics' })).toHaveAttribute(
      'aria-current',
      'step',
    );
  });

  it('sums the cost lines live and saves the draft with that total as the price', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/destinations$/, sampleDestinations],
      [/\/api\/v1\/me\/profile$/, hostProfile(null)],
      [/\/api\/v1\/trips$/, { id: 55 }, 201],
    ]);

    renderScreen(<TripWizardPage />);

    await chooseOption(user, screen.getByLabelText('Destination'), 'sajek');
    await user.type(screen.getByLabelText('Trip title'), 'Sajek sunrise weekend');
    await user.type(
      screen.getByLabelText('What the trip is like'),
      'Three days above the clouds with a jeep ride.',
    );
    await user.type(screen.getByLabelText('First day'), inDays(10));
    await user.type(screen.getByLabelText('Last day'), inDays(11));
    await user.type(screen.getByLabelText('Meeting point'), 'Arambagh, Dhaka');
    await user.click(screen.getByRole('button', { name: 'Next →' }));

    const amounts = await screen.findAllByLabelText('Per person (Tk)');
    await user.clear(amounts[0]!);
    await user.type(amounts[0]!, '2800');
    await user.clear(amounts[1]!);
    await user.type(amounts[1]!, '1800');
    await user.clear(amounts[2]!);
    await user.type(amounts[2]!, '1500');

    const total = screen.getByText('Total per person').parentElement!;
    expect(within(total).getByText('Tk 6,100')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Next →' }));
    const titles = await screen.findAllByLabelText('Title');
    const plans = screen.getAllByLabelText('Plan for the day');
    expect(titles).toHaveLength(2);
    for (const [index, title] of titles.entries()) {
      await user.type(title, `Day ${index + 1}`);
      await user.type(plans[index]!, 'Walk and look around.');
    }

    await user.click(screen.getByRole('button', { name: 'Next →' }));
    expect(
      await screen.findByText(
        'You can save a draft now. To publish, complete the national ID and selfie check.',
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Save draft' }));

    await waitFor(() => {
      const sent = requests(fetchMock).find(
        (request) => request.method === 'POST' && request.url.endsWith('/api/v1/trips'),
      );
      expect(sent).toBeDefined();
      const body = JSON.parse(sent!.body!) as { pricePerPerson: number; itinerary: unknown[] };
      expect(body.pricePerPerson).toBe(6100);
      expect(body.itinerary).toHaveLength(2);
    });
  }, 20_000); // Types a whole trip through four steps; slow under a parallel run.
});

describe('HostTripsPage', () => {
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

  it('shows each trip with its status and offers publishing a draft', async () => {
    stubApi([
      [
        /\/api\/v1\/me\/trips$/,
        [
          {
            id: 55,
            title: 'Sajek sunrise weekend',
            destination: sampleDestinations[0],
            startDate: '2026-10-20',
            endDate: '2026-10-22',
            seats: 12,
            seatsTaken: 0,
            pricePerPerson: 6100,
            groupType: 'Open',
            status: 'Draft',
            pendingRequests: 0,
          },
        ],
      ],
    ]);

    renderScreen(<HostTripsPage />);

    expect(await screen.findByText('Sajek sunrise weekend')).toBeInTheDocument();
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publish' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Edit' })).toHaveAttribute(
      'href',
      '/host/trips/55/edit',
    );
  });

  it('invites a new host to create their first trip', async () => {
    stubApi([[/\/api\/v1\/me\/trips$/, []]]);

    renderScreen(<HostTripsPage />);

    expect(await screen.findByText('You have not created a trip yet.')).toBeInTheDocument();
  });
});

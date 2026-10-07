import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { VerificationPage } from './VerificationPage';
import { useAuthStore } from './authStore';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import { requests, stubApi, stubResponse } from '@/test/fetchStub';

// The browser upload goes straight to storage (XHR) and the canvas resize needs a real browser;
// both are covered elsewhere, so here they simply succeed.
vi.mock('@/features/feed/feedApi', () => ({ uploadToStorage: vi.fn(() => Promise.resolve()) }));
vi.mock('@/lib/images', () => ({
  prepareImage: (file: File) => Promise.resolve(file),
  acceptedImageTypes: 'image/jpeg,image/png,image/webp',
}));

const profile = {
  userId: 1,
  maskedEmail: 'n****t@example.com',
  displayName: 'Nusrat Jahan',
  gender: 'Female',
  phone: null,
  bio: null,
  homeDistrict: null,
  emergencyContactName: null,
  emergencyContactPhone: null,
  roles: ['Traveler'],
  verifiedLevel: null,
  memberSince: '2026-01-10',
};

const ready = (id: number, kind: string) => ({
  id,
  kind,
  status: 'Ready',
  failureReason: null,
  created: '2026-10-06T06:00:00Z',
});

const approved = {
  id: 3,
  level: 'Nid',
  status: 'Approved',
  reason: null,
  created: '2026-10-06T06:00:00Z',
  reviewedOn: null,
};

describe('VerificationPage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'n****t@example.com', displayName: 'Nusrat Jahan' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('waits for both sides of the NID before the check can be sent', async () => {
    stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification\/documents$/, [ready(11, 'NidFront')]],
    ]);

    renderScreen(<VerificationPage />);

    expect(await screen.findByText(/Still needed: Back of the NID/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Check my ID' })).toBeDisabled();
  });

  it('sends the number, date of birth and photos, and shows the badge when approved', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification\/documents$/, [ready(11, 'NidFront'), ready(12, 'NidBack')]],
      [/\/api\/v1\/me\/verification$/, approved],
    ]);

    renderScreen(<VerificationPage />);
    await user.type(screen.getByLabelText('National ID number'), '1234567890');
    await user.type(screen.getByLabelText('Date of birth'), '1995-04-12');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Check my ID' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: 'Check my ID' }));

    expect(await screen.findByText('You are verified.')).toBeInTheDocument();
    const sent = requests(fetchMock).find(
      (request) => request.method === 'POST' && request.url.endsWith('/api/v1/me/verification'),
    );
    expect(JSON.parse(sent!.body!)).toEqual({
      level: 'Nid',
      idType: 'Nid',
      idNumber: '1234567890',
      dateOfBirth: '1995-04-12',
      documentIds: [11, 12],
    });
  });

  it('rejects a malformed NID without calling the API', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification\/documents$/, [ready(11, 'NidFront'), ready(12, 'NidBack')]],
    ]);

    renderScreen(<VerificationPage />);
    await user.type(screen.getByLabelText('National ID number'), '12345');
    await user.type(screen.getByLabelText('Date of birth'), '1995-04-12');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Check my ID' })).toBeEnabled());
    await user.click(screen.getByRole('button', { name: 'Check my ID' }));

    expect(
      await screen.findByText('Enter the 10, 13 or 17 digit number from your national ID card.'),
    ).toBeInTheDocument();
    expect(requests(fetchMock).some((request) => request.method === 'POST')).toBe(false);
  });

  it('asks for the passport photo page instead when a passport is chosen', async () => {
    const user = userEvent.setup();
    stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification\/documents$/, []],
    ]);

    renderScreen(<VerificationPage />);
    await user.selectOptions(await screen.findByLabelText('ID document'), 'Passport');

    expect(screen.getByLabelText('Passport number')).toBeInTheDocument();
    expect(screen.getByText('Passport photo page')).toBeInTheDocument();
    expect(screen.queryByText('Front of the NID')).not.toBeInTheDocument();
  });

  it('uploads a chosen photo and has the server check it', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification\/documents\/21\/complete$/, ready(21, 'NidFront')],
      [/\/api\/v1\/me\/verification\/documents$/, []],
    ]);

    // One URL, two meanings: GET lists the photos, POST asks for an upload link.
    const routed = fetchMock.getMockImplementation()!;
    const ticket = {
      id: '21',
      uploadUrl: 'https://storage.test/u1/x.upload?sig=w',
      expiresOn: '2026-10-06T07:00:00Z',
    };
    fetchMock.mockImplementation((input, init) =>
      init?.method === 'POST' &&
      typeof input === 'string' &&
      input.endsWith('/verification/documents')
        ? Promise.resolve(stubResponse(ticket))
        : routed(input, init),
    );

    renderScreen(<VerificationPage />);
    const photo = new File(['jpeg-bytes'], 'front.jpg', { type: 'image/jpeg' });
    await user.upload(await screen.findByLabelText('Front of the NID'), photo);

    await waitFor(() =>
      expect(
        requests(fetchMock).some((request) => request.url.endsWith('/documents/21/complete')),
      ).toBe(true),
    );
    const started = requests(fetchMock).find(
      (request) => request.method === 'POST' && request.url.endsWith('/verification/documents'),
    );
    expect(JSON.parse(started!.body!)).toEqual({
      kind: 'NidFront',
      contentType: 'image/jpeg',
      sizeBytes: photo.size,
    });
  });
});

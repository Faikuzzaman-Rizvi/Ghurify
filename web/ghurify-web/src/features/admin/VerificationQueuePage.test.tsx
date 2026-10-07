import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { VerificationQueuePage } from './VerificationQueuePage';
import { RoleRoute } from '@/features/auth/RoleRoute';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

const queue = {
  items: [
    {
      id: 11,
      userId: 4,
      displayName: 'Rafiq Chowdhury',
      maskedEmail: 'r****q@example.com',
      level: 'NidSelfie',
      status: 'Pending',
      provider: 'fake',
      providerRef: 'fake-1',
      reason: null,
      created: '2026-10-05T06:00:00Z',
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 25,
};

function profileWith(roles: string[]) {
  return {
    userId: 9,
    maskedEmail: 'a****n@example.com',
    displayName: 'Admin',
    gender: 'Male',
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

describe('VerificationQueuePage', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 9, maskedEmail: 'a****n@example.com', displayName: 'Admin' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists pending checks and approves one', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/verifications\/11\/review$/, null, 204],
      [/\/api\/v1\/admin\/verifications/, queue],
    ]);

    renderScreen(<VerificationQueuePage />);
    await user.click(await screen.findByRole('button', { name: 'Approve' }));

    await waitFor(() =>
      expect(
        requests(fetchMock).some(
          (request) =>
            request.url.endsWith('/review') && request.body === '{"approve":true,"reason":null}',
        ),
      ).toBe(true),
    );
  });

  it('shows the ID photos only when asked, from short-lived links', async () => {
    const user = userEvent.setup();
    const withPhotos = {
      ...queue,
      items: [{ ...queue.items[0], idType: 'Nid', documentCount: 2 }],
    };
    const fetchMock = stubApi([
      [
        /\/api\/v1\/admin\/verifications\/11\/documents$/,
        [
          {
            id: 1,
            kind: 'NidFront',
            url: 'https://storage.test/a.jpg?sig=r',
            purged: false,
            created: '2026-10-05T06:00:00Z',
          },
          { id: 2, kind: 'NidBack', url: null, purged: true, created: '2026-10-05T06:00:00Z' },
        ],
      ],
      [/\/api\/v1\/admin\/verifications/, withPhotos],
    ]);

    renderScreen(<VerificationQueuePage />);
    await user.click(await screen.findByRole('button', { name: 'View ID photos' }));

    expect(await screen.findByRole('img', { name: 'Front of the NID' })).toHaveAttribute(
      'src',
      'https://storage.test/a.jpg?sig=r',
    );
    expect(screen.getByText('Deleted after the decision')).toBeInTheDocument();
    expect(
      requests(fetchMock).filter((request) => request.url.endsWith('/documents')),
    ).toHaveLength(1);
  });

  it('will not reject without a reason', async () => {
    stubApi([[/\/api\/v1\/admin\/verifications/, queue]]);

    renderScreen(<VerificationQueuePage />);

    expect(await screen.findByRole('button', { name: 'Reject' })).toBeDisabled();
  });

  it('is hidden from someone without the admin role', async () => {
    stubApi([[/\/api\/v1\/me\/profile$/, profileWith(['Traveler', 'Host'])]]);

    renderScreen(
      <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
        <VerificationQueuePage />
      </RoleRoute>,
    );

    expect(await screen.findByText('You are not allowed to do this.')).toBeInTheDocument();
    expect(screen.queryByText('Verification queue')).not.toBeInTheDocument();
  });
});

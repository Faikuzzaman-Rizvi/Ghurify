import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AccountPage } from './AccountPage';
import i18n from '@/i18n';
import { useAuthStore } from './authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

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

describe('AccountPage', () => {
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

  it('shows the profile and offers identity verification to someone unverified', async () => {
    stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification$/, []],
    ]);

    renderScreen(<AccountPage />);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Nusrat Jahan' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Verify my identity' })).toHaveAttribute(
      'href',
      '/account/verify',
    );
    expect(screen.getByRole('button', { name: 'Become a host' })).toBeInTheDocument();
  });

  it('names why a profile picture was refused, rather than blaming the connection', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification$/, []],
    ]);

    const { container } = renderScreen(<AccountPage />);
    await screen.findByRole('heading', { level: 1, name: 'Nusrat Jahan' });

    const picker = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    // Over the 3 MB the API allows for a profile picture.
    const tooBig = new File([new Uint8Array(3.5 * 1024 * 1024)], 'holiday.jpg', {
      type: 'image/jpeg',
    });
    await user.upload(picker, tooBig);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'That photo is too large. Choose a smaller one.',
    );
    // Nothing was sent: the file could never have been accepted.
    expect(requests(fetchMock).some((request) => request.url.includes('/me/avatar'))).toBe(false);
  });

  it('refuses a phone number that is not a Bangladeshi mobile before calling the API', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile],
      [/\/api\/v1\/me\/verification$/, []],
    ]);

    renderScreen(<AccountPage />);
    await user.type(await screen.findByLabelText('Mobile number', { selector: '#phone' }), '12345');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(
      await screen.findByText('Enter a Bangladeshi mobile number, for example 01712345678.'),
    ).toBeInTheDocument();
    expect(requests(fetchMock).some((request) => request.method === 'PUT')).toBe(false);
  });

  it('locks gender once the identity check has passed', async () => {
    stubApi([
      [/\/api\/v1\/me\/profile$/, { ...profile, verifiedLevel: 'Nid' }],
      [/\/api\/v1\/me\/verification$/, []],
    ]);

    renderScreen(<AccountPage />);

    expect(await screen.findByLabelText('Gender')).toBeDisabled();
    expect(screen.getByText('ID verified')).toBeInTheDocument();
  });

  it('shows the translated reason when the API rejects a duplicate phone number', async () => {
    const user = userEvent.setup();
    stubApi([
      [/\/api\/v1\/me\/verification$/, []],
      [/\/api\/v1\/me\/profile$/, profile],
    ]);

    renderScreen(<AccountPage />);
    await screen.findByRole('heading', { level: 1 });

    stubApi([
      [/\/api\/v1\/me\/verification$/, []],
      [/\/api\/v1\/me\/profile$/, { title: 'Conflict', code: 'phone_in_use' }, 409],
    ]);
    await user.type(screen.getByLabelText('Mobile number', { selector: '#phone' }), '01712345678');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(
      await screen.findByText('This phone number is already used by another account.'),
    ).toBeInTheDocument();
  });
});

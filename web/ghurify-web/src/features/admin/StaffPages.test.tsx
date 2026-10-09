import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { StaffMembersPage } from './StaffMembersPage';
import { StaffRolesPage } from './StaffRolesPage';
import { clearStepUpReceipt } from './stepUp';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

/** The catalogue as the API serves it: the screen renders this, not a list of its own. */
const catalogue = [
  { key: 'dashboard.view', group: 'Overview', requiresStepUp: false },
  { key: 'payments.view', group: 'Money', requiresStepUp: false },
  { key: 'payouts.approve', group: 'Money', requiresStepUp: false },
  { key: 'staff.roles.manage', group: 'Staff', requiresStepUp: true },
];

const superAdminRole = {
  id: 1,
  key: 'super-admin',
  name: 'Super admin',
  nameBn: 'সুপার অ্যাডমিন',
  description: 'Complete control.',
  descriptionBn: 'সম্পূর্ণ নিয়ন্ত্রণ।',
  isSystem: true,
  isSuperAdmin: true,
  memberCount: 1,
  permissions: [],
  created: '2026-01-01T00:00:00Z',
};

const paymentsRole = {
  id: 5,
  key: 'payments-desk',
  name: 'Payments desk',
  nameBn: 'পেমেন্ট ডেস্ক',
  description: null,
  descriptionBn: null,
  isSystem: false,
  isSuperAdmin: false,
  memberCount: 0,
  permissions: ['payments.view'],
  created: '2026-02-01T00:00:00Z',
};

/** The signed-in person's own profile, which decides what the screens offer. */
function profile(permissions: string[], isSuperAdmin = false) {
  return {
    userId: 1,
    maskedEmail: 'a****n@example.com',
    displayName: 'Admin',
    gender: 'Female',
    phone: null,
    bio: null,
    homeDistrict: null,
    emergencyContactName: null,
    emergencyContactPhone: null,
    roles: ['Traveler'],
    verifiedLevel: 'NidSelfie',
    memberSince: '2026-01-01',
    avatarVersion: null,
    permissions,
    isSuperAdmin,
    staffRoles: [],
  };
}

describe('Super admin: roles and the desk', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'a****n@example.com', displayName: 'Admin' },
    });
    clearStepUpReceipt();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearStepUpReceipt();
  });

  it('lists each role with what it may do, and says the super admin needs no list', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/staff\/roles/,
        {
          permissions: catalogue,
          grantablePermissions: catalogue.map((permission) => permission.key),
          roles: [superAdminRole, paymentsRole],
        },
      ],
    ]);

    renderScreen(<StaffRolesPage />);

    expect(await screen.findByText('Super admin')).toBeInTheDocument();
    expect(screen.getByText('Payments desk')).toBeInTheDocument();

    // A super admin's power cannot be expressed as a list, and the screen says so.
    expect(screen.getByText(/may do everything/i)).toBeInTheDocument();

    // The ordinary role's permissions read as words, not keys.
    expect(
      screen.getByText('See payments and their gateway history'),
    ).toBeInTheDocument();
  });

  it('offers no edit or delete for the super admin role, and no delete for a built-in one', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/staff\/roles/,
        {
          permissions: catalogue,
          grantablePermissions: catalogue.map((permission) => permission.key),
          roles: [
            superAdminRole,
            { ...paymentsRole, id: 2, key: 'admin', name: 'Admin', isSystem: true },
          ],
        },
      ],
    ]);

    renderScreen(<StaffRolesPage />);

    await screen.findByText('Super admin');

    expect(screen.queryByRole('button', { name: /Edit Super admin/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete Super admin/i })).not.toBeInTheDocument();
    // A built-in role can be retuned but never removed.
    expect(screen.getByRole('button', { name: /Edit Admin/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete Admin/i })).not.toBeInTheDocument();
  });

  it('cannot tick a permission the signed-in person does not hold', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/staff\/roles/,
        {
          permissions: catalogue,
          // Everything except the two the caller lacks.
          grantablePermissions: ['dashboard.view', 'payments.view'],
          roles: [paymentsRole],
        },
      ],
    ]);

    renderScreen(<StaffRolesPage />);

    await userEvent.click(await screen.findByRole('button', { name: /Edit Payments desk/i }));

    expect(
      screen.getByRole('checkbox', { name: /See payments and their gateway history/i }),
    ).toBeEnabled();
    expect(
      screen.getByRole('checkbox', { name: /Confirm a payout has been sent/i }),
    ).toBeDisabled();
    expect(
      screen.getByRole('checkbox', { name: /Create roles and choose what each may do/i }),
    ).toBeDisabled();
  });

  it('asks for the password when saving a role, then retries the save', async () => {
    const fetchMock = stubApi([
      // The first PUT is refused for want of a receipt; the stub answers the same way every
      // time, so the retry proves the password was asked for and the call repeated.
      [/\/api\/v1\/admin\/step-up/, { token: 'receipt', expiresOn: '2099-01-01T00:00:00Z', expiresInSeconds: 600 }],
      [/\/api\/v1\/admin\/staff\/roles\/5/, { title: 'Confirm it is you', code: 'step_up_required' }, 403],
      [
        /\/api\/v1\/admin\/staff\/roles/,
        {
          permissions: catalogue,
          grantablePermissions: catalogue.map((permission) => permission.key),
          roles: [paymentsRole],
        },
      ],
    ]);

    renderScreen(<StaffRolesPage />);

    await userEvent.click(await screen.findByRole('button', { name: /Edit Payments desk/i }));
    await userEvent.click(screen.getByRole('checkbox', { name: /Confirm a payout has been sent/i }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    // The portal asks rather than failing.
    const password = await screen.findByLabelText('Your password');
    await userEvent.type(password, 'correct horse battery');
    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => {
      const puts = requests(fetchMock).filter((request) => request.method === 'PUT');
      expect(puts).toHaveLength(2);
    });

    const puts = requests(fetchMock).filter((request) => request.method === 'PUT');
    // The same change both times: the form was not lost.
    expect(puts.map((request) => request.body)).toEqual([
      expect.stringContaining('payouts.approve'),
      expect.stringContaining('payouts.approve'),
    ]);
  });

  it('shows who is on the desk, and who put them there', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/staff\/members/,
        {
          members: [
            {
              userId: 7,
              email: 'rumi@example.com',
              displayName: 'Rumi Hasan',
              status: 'Active',
              avatarUpdatedOn: null,
              staffSince: '2026-05-01T00:00:00Z',
              roles: [
                {
                  staffRoleId: 2,
                  key: 'admin',
                  name: 'Admin',
                  nameBn: 'অ্যাডমিন',
                  isSuperAdmin: false,
                  grantedOn: '2026-05-01T00:00:00Z',
                  grantedById: 1,
                  grantedByName: 'Nusrat',
                },
              ],
            },
          ],
        },
      ],
      [
        /\/api\/v1\/admin\/staff\/roles/,
        { permissions: catalogue, grantablePermissions: [], roles: [superAdminRole] },
      ],
      [/\/api\/v1\/me\/profile/, profile(['staff.view', 'staff.assign'])],
    ]);

    renderScreen(<StaffMembersPage />);

    expect(await screen.findByRole('link', { name: 'Rumi Hasan' })).toHaveAttribute(
      'href',
      '/admin/users/7',
    );
    expect(screen.getByText('rumi@example.com')).toBeInTheDocument();
    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('by Nusrat')).toBeInTheDocument();
  });

  it('offers no way to change the desk without the assign permission', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/staff\/members/,
        {
          members: [
            {
              userId: 7,
              email: 'rumi@example.com',
              displayName: 'Rumi Hasan',
              status: 'Active',
              avatarUpdatedOn: null,
              staffSince: '2026-05-01T00:00:00Z',
              roles: [
                {
                  staffRoleId: 2,
                  key: 'admin',
                  name: 'Admin',
                  nameBn: 'অ্যাডমিন',
                  isSuperAdmin: false,
                  grantedOn: '2026-05-01T00:00:00Z',
                  grantedById: null,
                  grantedByName: null,
                },
              ],
            },
          ],
        },
      ],
      [
        /\/api\/v1\/admin\/staff\/roles/,
        { permissions: catalogue, grantablePermissions: [], roles: [superAdminRole] },
      ],
      // Can look, cannot touch.
      [/\/api\/v1\/me\/profile/, profile(['staff.view'])],
    ]);

    renderScreen(<StaffMembersPage />);

    await screen.findByRole('link', { name: 'Rumi Hasan' });

    expect(screen.queryByRole('button', { name: /Add somebody/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Take Rumi Hasan off/i })).not.toBeInTheDocument();
  });
});

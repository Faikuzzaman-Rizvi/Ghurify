import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ForgotPasswordPage } from './ForgotPasswordPage';
import { LoginPage } from './LoginPage';
import { PasswordCard } from './PasswordCard';
import { RegisterPage } from './RegisterPage';
import { useAuthStore } from './authStore';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

const session = {
  accessToken: 'access-token',
  expiresInSeconds: 900,
  user: { id: 7, maskedEmail: 'r****i@example.com', displayName: 'Rizvi' },
};

const codeSent = { expiresInSeconds: 300, resendAfterSeconds: 60 };

function bodyOf(fetchMock: ReturnType<typeof stubApi>, path: string): unknown {
  const sent = requests(fetchMock).find((request) => request.url.endsWith(path));
  return sent?.body ? JSON.parse(sent.body) : undefined;
}

describe('Account screens', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({ status: 'anonymous', user: null });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('signs in with email and password, and no code', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([[/\/api\/v1\/auth\/sign-in$/, session]]);

    renderScreen(<LoginPage />, { at: '/login', path: '/login' });

    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'monsoon tea garden walk');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'));
    expect(bodyOf(fetchMock, '/auth/sign-in')).toEqual({
      email: 'rizvi@example.com',
      password: 'monsoon tea garden walk',
    });
    expect(requests(fetchMock).some((request) => request.url.includes('/register'))).toBe(false);
  });

  it('rejects an address that is not an email, without calling the API', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([]);

    renderScreen(<LoginPage />, { at: '/login', path: '/login' });

    await user.type(screen.getByLabelText('Email address'), 'not-an-email');
    await user.type(screen.getByLabelText('Password'), 'whatever password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText(/Enter a valid email address/)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('shows the same message for a wrong password, and how long a pause lasts', async () => {
    const user = userEvent.setup();
    stubApi([
      [
        /\/api\/v1\/auth\/sign-in$/,
        { title: 'Too many attempts', code: 'sign_in_paused', retryAfterSeconds: 840 },
        429,
      ],
    ]);

    renderScreen(<LoginPage />, { at: '/login', path: '/login' });

    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText(/Try again in 14 minutes/)).toBeInTheDocument();
  });

  it('lets the password be shown and hidden', async () => {
    const user = userEvent.setup();
    stubApi([]);

    renderScreen(<LoginPage />, { at: '/login', path: '/login' });

    const password = screen.getByLabelText('Password');
    expect(password).toHaveAttribute('type', 'password');
    await user.click(screen.getByRole('button', { name: 'Show password' }));
    expect(password).toHaveAttribute('type', 'text');
  });

  it('offers a new code when the address was never confirmed', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/auth\/register\/resend$/, codeSent, 202],
      [
        /\/api\/v1\/auth\/sign-in$/,
        { title: 'Email not confirmed', code: 'email_not_confirmed' },
        403,
      ],
    ]);

    renderScreen(<LoginPage />, { at: '/login', path: '/login' });

    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'monsoon tea garden walk');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    await user.click(await screen.findByRole('button', { name: 'Send me a new code' }));

    await waitFor(() =>
      expect(bodyOf(fetchMock, '/register/resend')).toEqual({ email: 'rizvi@example.com' }),
    );
  });

  it('creates an account, then confirms the email with the code and signs in', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/auth\/register\/confirm$/, session],
      [/\/api\/v1\/auth\/register$/, codeSent, 202],
    ]);

    renderScreen(<RegisterPage />, { at: '/register', path: '/register' });

    await user.type(screen.getByLabelText('Your full name'), 'Rizvi Ahmed');
    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'monsoon tea garden walk');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('We sent a code to rizvi@example.com.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Send a new code in/ })).toBeDisabled();

    await user.type(screen.getByLabelText('Six-digit code'), '123456');
    await user.click(screen.getByRole('button', { name: 'Confirm and continue' }));

    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'));
    expect(bodyOf(fetchMock, '/auth/register')).toEqual({
      displayName: 'Rizvi Ahmed',
      email: 'rizvi@example.com',
      password: 'monsoon tea garden walk',
    });
  });

  it('refuses a short password before calling the API', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([]);

    renderScreen(<RegisterPage />, { at: '/register', path: '/register' });

    await user.type(screen.getByLabelText('Your full name'), 'Rizvi Ahmed');
    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'short');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('Use at least 10 characters.')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('explains a password the API says is too common', async () => {
    const user = userEvent.setup();
    stubApi([[/\/api\/v1\/auth\/register$/, { title: 'Weak', code: 'password_too_common' }, 422]]);

    renderScreen(<RegisterPage />, { at: '/register', path: '/register' });

    await user.type(screen.getByLabelText('Your full name'), 'Rizvi Ahmed');
    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.type(screen.getByLabelText('Password'), 'bangladesh123');
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText(/too common or too close to your email/)).toBeInTheDocument();
  });

  it('resets a forgotten password with the emailed code', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/auth\/password\/forgot$/, codeSent, 202],
      [/\/api\/v1\/auth\/password\/reset$/, session],
    ]);

    renderScreen(<ForgotPasswordPage />, { at: '/forgot-password', path: '/forgot-password' });

    await user.type(screen.getByLabelText('Email address'), 'rizvi@example.com');
    await user.click(screen.getByRole('button', { name: 'Send reset code' }));

    await user.type(await screen.findByLabelText('Six-digit code'), '654321');
    await user.type(screen.getByLabelText('New password'), 'new river crossing plan');
    await user.click(screen.getByRole('button', { name: 'Save password and sign in' }));

    await waitFor(() => expect(useAuthStore.getState().status).toBe('authenticated'));
    expect(bodyOf(fetchMock, '/password/reset')).toEqual({
      email: 'rizvi@example.com',
      code: '654321',
      newPassword: 'new river crossing plan',
    });
  });

  it('changes the password from the account page', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 7, maskedEmail: 'r****i@example.com', displayName: 'Rizvi' },
    });
    const fetchMock = stubApi([[/\/api\/v1\/auth\/password\/change$/, session]]);

    renderScreen(<PasswordCard />);

    await user.type(screen.getByLabelText('Current password'), 'old trusty password');
    await user.type(screen.getByLabelText('New password'), 'hill station mornings');
    await user.click(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText(/Your other devices were signed out/)).toBeInTheDocument();
    expect(bodyOf(fetchMock, '/password/change')).toEqual({
      currentPassword: 'old trusty password',
      newPassword: 'hill station mornings',
    });
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useLocation } from 'react-router';

import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requestedUrls, requests, stubApi } from '@/test/fetchStub';
import { SiteHeader } from './SiteHeader';

function signedIn() {
  useAuthStore.setState({
    status: 'authenticated',
    user: { id: 1, maskedEmail: 'r***@example.com', displayName: 'Rizvi' },
  });
}

function profile({ avatarVersion }: { avatarVersion: number | null }) {
  return {
    userId: 1,
    displayName: 'Rizvi',
    maskedEmail: 'r***@example.com',
    roles: ['Traveler'],
    avatarVersion,
  };
}

/** Where the router is, so a test can see a navigation. */
function CurrentAddress() {
  return <span data-testid="address">{useLocation().pathname}</span>;
}

describe('SiteHeader', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    stubApi([]);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    useAuthStore.setState({ status: 'unknown', user: null });
  });

  it('offers sign-in to a visitor', () => {
    useAuthStore.setState({ status: 'anonymous', user: null });

    renderScreen(<SiteHeader />);

    expect(screen.getAllByRole('link', { name: /Sign in/ }).length).toBeGreaterThan(0);
  });

  it('opens the menu with the main sections and closes it again', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({ status: 'anonymous', user: null });

    renderScreen(<SiteHeader />);
    await user.click(screen.getByRole('button', { name: 'Open menu' }));

    const menu = screen.getByRole('dialog', { name: 'Menu' });
    expect(within(menu).getByRole('link', { name: 'Explore trips' })).toHaveAttribute(
      'href',
      '/trips',
    );
    expect(within(menu).getByRole('link', { name: /Sign in/ })).toBeInTheDocument();

    await user.click(within(menu).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog', { name: 'Menu' })).not.toBeInTheDocument();
  });

  it('shows the profile picture in the account menu once the profile has one', async () => {
    stubApi([[/\/api\/v1\/me\/profile$/, profile({ avatarVersion: 1_760_000_000 })]]);
    signedIn();

    renderScreen(<SiteHeader />);

    const menuButton = screen.getByRole('button', { name: 'Your account menu' });
    await waitFor(() =>
      expect(menuButton.querySelector('img')).toHaveAttribute(
        'src',
        '/api/v1/users/1/avatar?v=1760000000',
      ),
    );
  });

  it('shows the initial, and asks for no picture, when there is none', async () => {
    const fetchMock = stubApi([[/\/api\/v1\/me\/profile$/, profile({ avatarVersion: null })]]);
    signedIn();

    renderScreen(<SiteHeader />);
    await waitFor(() =>
      expect(requestedUrls(fetchMock).some((url) => url.endsWith('/me/profile'))).toBe(true),
    );

    const menuButton = screen.getByRole('button', { name: 'Your account menu' });
    expect(menuButton).toHaveTextContent('R');
    expect(menuButton.querySelector('img')).toBeNull();
    expect(requestedUrls(fetchMock).some((url) => url.includes('/avatar'))).toBe(false);
  });

  it('signs out from the account menu and goes to the home page', async () => {
    const user = userEvent.setup();
    const fetchMock = stubApi([
      [/\/api\/v1\/me\/profile$/, profile({ avatarVersion: null })],
      [/\/api\/v1\/auth\/logout$/, undefined, 204],
    ]);
    signedIn();

    renderScreen(
      <>
        <SiteHeader />
        <CurrentAddress />
      </>,
      { at: '/me/trips' },
    );
    await user.click(screen.getByRole('button', { name: 'Your account menu' }));
    expect(screen.getByText('r***@example.com')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(useAuthStore.getState().status).toBe('anonymous'));
    await waitFor(() => expect(screen.getByTestId('address')).toHaveTextContent(/^\/$/));
    expect(
      requests(fetchMock).some(
        (request) => request.url.endsWith('/auth/logout') && request.method === 'POST',
      ),
    ).toBe(true);
    expect(screen.getAllByRole('link', { name: /Sign in/ }).length).toBeGreaterThan(0);
  });

  it('puts the signed-in destinations in the account menu, not sign-in', async () => {
    const user = userEvent.setup();
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'r***@example.com', displayName: 'Rizvi' },
    });

    renderScreen(<SiteHeader />);
    await user.click(screen.getByRole('button', { name: 'Your account menu' }));

    expect(screen.getByText('Rizvi')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'My trips' })).toHaveAttribute('href', '/me/trips');
    expect(screen.queryByRole('link', { name: /Sign in/ })).not.toBeInTheDocument();
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { stubApi } from '@/test/fetchStub';
import { SiteHeader } from './SiteHeader';

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

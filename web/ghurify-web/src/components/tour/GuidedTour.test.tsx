import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { GuidedTour } from './GuidedTour';
import { hasSeenTour, useTourStore } from './tourStore';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';

describe('GuidedTour', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    window.localStorage.clear();
    useTourStore.setState({ active: false, step: 0 });
  });

  afterEach(() => {
    act(() => useTourStore.setState({ active: false, step: 0 }));
  });

  it('shows nothing until it is started', () => {
    renderScreen(<GuidedTour />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('walks through the steps and remembers that it was seen', async () => {
    const user = userEvent.setup();
    renderScreen(<GuidedTour />);

    act(() => useTourStore.getState().start());

    expect(screen.getByRole('dialog', { name: 'Welcome to GhuriFiri' })).toBeInTheDocument();
    expect(screen.getByText(/Step 1 of 7/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Next' }));
    expect(screen.getByRole('dialog', { name: 'Search for a trip' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Back' }));
    expect(screen.getByRole('dialog', { name: 'Welcome to GhuriFiri' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Skip tour' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(hasSeenTour()).toBe(true);
  });

  it('closes on Escape', async () => {
    const user = userEvent.setup();
    renderScreen(<GuidedTour />);

    act(() => useTourStore.getState().start());
    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('speaks Bangla when the language is Bangla', async () => {
    await i18n.changeLanguage('bn');
    renderScreen(<GuidedTour />);

    act(() => useTourStore.getState().start());

    expect(screen.getByRole('dialog', { name: 'ঘুরিফিরিতে স্বাগতম' })).toBeInTheDocument();
  });
});

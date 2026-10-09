import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { BrandingPage } from './BrandingPage';
import { ThemePage } from './ThemePage';
import { contrastProblems } from './contrast';
import i18n from '@/i18n';
import { useAuthStore } from '@/features/auth/authStore';
import { renderScreen } from '@/test/render';
import { requests, stubApi } from '@/test/fetchStub';

/** One setting in the shape the API serves. */
function setting(
  key: string,
  group: string,
  kind: string,
  value: string,
  fallback = value,
) {
  return {
    key,
    group,
    kind,
    value,
    default: fallback,
    isCustom: value !== fallback,
    required: false,
    maxLength: 200,
  };
}

/** The shipped colours, which are the ones the contrast rules are tuned against. */
const colours = {
  'theme.colour.hill': '#245c43',
  'theme.colour.deep': '#173f2e',
  'theme.colour.night': '#0f2a1f',
  'theme.colour.turmeric': '#d99a12',
  'theme.colour.ochre': '#9a6500',
  'theme.colour.jamdani': '#a3305c',
  'theme.colour.mist': '#f1f5f2',
};

function settingsPage(editableGroups = ['Identity', 'Contact', 'Social', 'Theme']) {
  return {
    editableGroups,
    bodyFonts: ['Barlow', 'Inter'],
    displayFonts: ['Poppins', 'Montserrat'],
    settings: [
      setting('site.name', 'Identity', 'Text', 'Ghurify'),
      setting('site.name.bn', 'Identity', 'Text', 'ঘুরিফাই'),
      setting('site.tagline', 'Identity', 'Text', 'Travel together, safely'),
      setting('contact.email', 'Contact', 'EmailAddress', ''),
      setting('social.facebook', 'Social', 'Url', ''),
      ...Object.entries(colours).map(([key, value]) =>
        setting(key, 'Theme', 'Colour', value),
      ),
      setting('theme.font.body', 'Theme', 'Font', 'Barlow'),
      setting('theme.font.display', 'Theme', 'Font', 'Poppins'),
    ],
    assets: [
      {
        kind: 'logo',
        url: '/favicon.svg',
        isCustom: false,
        maxBytes: 524288,
        sizeBytes: null,
        updatedOn: null,
      },
    ],
  };
}

describe('Super admin: branding and theme', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    useAuthStore.setState({
      status: 'authenticated',
      user: { id: 1, maskedEmail: 'a****n@example.com', displayName: 'Admin' },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends only the fields that changed', async () => {
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/settings$/, settingsPage()],
      [/\/api\/v1\/admin\/settings/, { changed: 1 }],
    ]);

    renderScreen(<BrandingPage />);

    const name = await screen.findByLabelText(/Website name \(English\)/);
    await userEvent.clear(name);
    await userEvent.type(name, 'Bhromon');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(requests(fetchMock).some((request) => request.method === 'PUT')).toBe(true);
    });

    const put = requests(fetchMock).find((request) => request.method === 'PUT');
    const body = JSON.parse(put!.body!) as { settings: Record<string, string> };

    // The one field that moved, and nothing else: an absent key means "leave it alone", so a
    // screenful of untouched settings is never rewritten.
    expect(body.settings).toEqual({ 'site.name': 'Bhromon' });
  });

  it('will not let an unreadable colour be saved', async () => {
    const fetchMock = stubApi([
      [/\/api\/v1\/admin\/settings$/, settingsPage()],
      [/\/api\/v1\/admin\/settings/, { changed: 1 }],
    ]);

    renderScreen(<ThemePage />);

    // A pale mint primary, which cannot carry text on a white page.
    const hill = await screen.findByLabelText(/^Primary$/);
    await userEvent.clear(hill);
    await userEvent.type(hill, '#e8f5ee');

    // The screen says which combinations fail, in the same place the colour was chosen.
    expect(await screen.findByText(/cannot be read/i)).toBeInTheDocument();
    expect(screen.getByText(/Primary text on white/)).toBeInTheDocument();

    // And the save is not offered at all, so the round trip is never made.
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(requests(fetchMock).some((request) => request.method === 'PUT')).toBe(false);
  });

  it('allows a readable colour and reports no problems', async () => {
    stubApi([
      [/\/api\/v1\/admin\/settings$/, settingsPage()],
      [/\/api\/v1\/admin\/settings/, { changed: 1 }],
    ]);

    renderScreen(<ThemePage />);

    const hill = await screen.findByLabelText(/^Primary$/);
    await userEvent.clear(hill);
    await userEvent.type(hill, '#1f4e7a');

    expect(await screen.findByText(/Every combination can be read/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
  });

  it('shows the theme read-only to somebody who may only edit the branding', async () => {
    stubApi([[/\/api\/v1\/admin\/settings$/, settingsPage(['Identity', 'Contact', 'Social'])]]);

    renderScreen(<ThemePage />);

    // Visible but not changeable: knowing the site's colours is useful even without the
    // permission to change them.
    expect(await screen.findByText(/see these settings but not change them/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Primary$/)).toBeDisabled();
  });

  it('offers a reset only for a setting that has been changed', async () => {
    stubApi([
      [
        /\/api\/v1\/admin\/settings$/,
        {
          ...settingsPage(),
          settings: [
            { ...setting('site.name', 'Identity', 'Text', 'Bhromon', 'Ghurify'), isCustom: true },
            setting('site.tagline', 'Identity', 'Text', 'Travel together, safely'),
          ],
        },
      ],
    ]);

    renderScreen(<BrandingPage />);

    await screen.findByLabelText(/Website name \(English\)/);
    // One reset button: for the renamed field only.
    expect(screen.getAllByRole('button', { name: 'Reset' })).toHaveLength(1);
  });
});

describe('the contrast rules the panel mirrors', () => {
  it('passes the colours the app shipped with', () => {
    const tokens = Object.fromEntries(
      Object.entries(colours).map(([key, value]) => [key.replace('theme.colour.', ''), value]),
    );

    expect(contrastProblems(tokens)).toEqual([]);
  });

  it('reports the pairs a faint primary breaks, and nothing else', () => {
    const tokens = Object.fromEntries(
      Object.entries(colours).map(([key, value]) => [key.replace('theme.colour.', ''), value]),
    );
    tokens['hill'] = '#e8f5ee';

    const problems = contrastProblems(tokens);

    expect(problems.length).toBeGreaterThan(0);
    expect(problems.every((problem) => problem.token === 'hill')).toBe(true);
  });

  it('ignores a half-typed colour rather than calling it a failure', () => {
    const tokens = Object.fromEntries(
      Object.entries(colours).map(([key, value]) => [key.replace('theme.colour.', ''), value]),
    );
    tokens['hill'] = '#24';

    // Mid-edit is not an answer yet, so it must not flash a warning on every keystroke.
    expect(contrastProblems(tokens).every((problem) => problem.token !== 'hill')).toBe(true);
  });
});

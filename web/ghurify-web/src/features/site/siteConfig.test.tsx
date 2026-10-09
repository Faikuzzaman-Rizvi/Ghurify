import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import { applyIdentity, applyTheme, clearTheme } from './applySiteConfig';
import type { SiteConfig } from './siteApi';
import { Logo } from '@/components/Logo';
import i18n from '@/i18n';
import { renderScreen } from '@/test/render';
import { stubApi } from '@/test/fetchStub';

/** A configuration in the shape the API serves. */
function config(overrides: Partial<SiteConfig> = {}): SiteConfig {
  return {
    version: 'abc123',
    identity: {
      name: 'Bhromon',
      nameBn: 'ভ্রমণ',
      tagline: 'Go further, together',
      taglineBn: 'আরও দূরে, একসাথে',
      description: 'Small-group trips.',
      descriptionBn: 'ছোট দলে ট্রিপ।',
    },
    contact: {
      email: 'hello@bhromon.test',
      phone: '+8801712345678',
      address: '12 Gulshan Avenue, Dhaka',
      addressBn: '১২ গুলশান অ্যাভিনিউ, ঢাকা',
    },
    social: [{ platform: 'facebook', url: 'https://facebook.com/bhromon' }],
    theme: {
      colours: {
        hill: '#1f4e7a',
        deep: '#10314d',
        night: '#081b2b',
        turmeric: '#d99a12',
        ochre: '#9a6500',
        jamdani: '#a3305c',
        mist: '#eef3f8',
      },
      bodyFont: 'Inter',
      displayFont: 'Montserrat',
      bodyFontStack: "'Inter', 'Hind Siliguri', sans-serif",
      displayFontStack: "'Montserrat', 'Hind Siliguri', sans-serif",
      fontStylesheet: 'https://fonts.googleapis.com/css2?family=Inter:wght@400&display=swap',
    },
    assets: [
      { kind: 'logo', url: '/favicon.svg', isCustom: false },
      { kind: 'logo-dark', url: '/favicon.svg', isCustom: false },
      { kind: 'favicon', url: '/favicon.svg', isCustom: false },
      { kind: 'apple-touch-icon', url: '/apple-touch-icon.png', isCustom: false },
      { kind: 'social-image', url: '', isCustom: false },
    ],
    ...overrides,
  };
}

describe('the site configuration', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
    document.head.innerHTML = '';
    document.documentElement.removeAttribute('style');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.documentElement.removeAttribute('style');
  });

  it('sets a CSS variable for every colour, which is what recolours the site', () => {
    applyTheme(config().theme);

    const style = document.documentElement.style;
    // The utilities in the stylesheet are all written as var(--color-hill), so redefining the
    // variable recolours every button and border on every page at once.
    expect(style.getPropertyValue('--color-hill')).toBe('#1f4e7a');
    expect(style.getPropertyValue('--color-mist')).toBe('#eef3f8');
    expect(style.getPropertyValue('--font-sans')).toContain('Inter');
    expect(style.getPropertyValue('--font-display')).toContain('Montserrat');
  });

  it('loads the chosen font families once, from Google Fonts only', () => {
    applyTheme(config().theme);
    applyTheme(config().theme);

    const links = document.head.querySelectorAll('link[rel="stylesheet"]');
    expect(links).toHaveLength(1);
    expect(links[0]?.getAttribute('href')).toContain('fonts.googleapis.com');
  });

  it('puts the colours back when a preview is closed', () => {
    const theme = config().theme;
    applyTheme(theme);
    clearTheme(Object.keys(theme.colours));

    expect(document.documentElement.style.getPropertyValue('--color-hill')).toBe('');
    expect(document.documentElement.style.getPropertyValue('--font-sans')).toBe('');
  });

  it('names the tab and the link preview after the site', () => {
    applyIdentity(config(), 'en');

    expect(document.title).toBe('Bhromon');
    expect(
      document.head.querySelector('meta[name="description"]')?.getAttribute('content'),
    ).toBe('Small-group trips.');
    expect(
      document.head.querySelector('meta[property="og:site_name"]')?.getAttribute('content'),
    ).toBe('Bhromon');
    // From the theme, so a recoloured site also recolours the browser chrome on a phone.
    expect(
      document.head.querySelector('meta[name="theme-color"]')?.getAttribute('content'),
    ).toBe('#10314d');
  });

  it('uses the Bangla name and description when the reader is in Bangla', () => {
    applyIdentity(config(), 'bn');

    expect(document.title).toBe('ভ্রমণ');
    expect(
      document.head.querySelector('meta[name="description"]')?.getAttribute('content'),
    ).toBe('ছোট দলে ট্রিপ।');
  });

  it('leaves the shipped icons alone until one is uploaded', () => {
    document.head.innerHTML = '<link rel="icon" href="/favicon.svg">';

    applyIdentity(config(), 'en');

    // Nothing is custom, so the links index.html set are untouched.
    expect(document.head.querySelectorAll('link[rel="icon"]')).toHaveLength(1);
    expect(document.head.querySelector('link[rel="icon"]')?.getAttribute('href')).toBe(
      '/favicon.svg',
    );
  });

  it('replaces the shipped icons with an uploaded one rather than adding to them', () => {
    // Two shipped links, as index.html has: an SVG and a PNG.
    document.head.innerHTML =
      '<link rel="icon" href="/favicon.svg"><link rel="icon" href="/favicon-32.png">';

    applyIdentity(
      config({
        assets: [
          { kind: 'favicon', url: '/api/v1/site/assets/favicon?v=99', isCustom: true },
        ] as SiteConfig['assets'],
      }),
      'en',
    );

    // One link, pointing at the upload. Leaving the old ones would let the browser keep
    // showing whichever it preferred.
    const icons = document.head.querySelectorAll('link[rel="icon"]');
    expect(icons).toHaveLength(1);
    expect(icons[0]?.getAttribute('href')).toBe('/api/v1/site/assets/favicon?v=99');
  });

  it('shows the uploaded logo in the header instead of the drawing', async () => {
    stubApi([[/\/api\/v1\/site\/config/, config({
      assets: [
        { kind: 'logo', url: '/api/v1/site/assets/logo?v=5', isCustom: true },
        { kind: 'logo-dark', url: '/api/v1/site/assets/logo-dark?v=5', isCustom: true },
      ] as SiteConfig['assets'],
    })]]);

    renderScreen(<Logo />);

    // The name beside the mark comes from the configuration too.
    const mark = await screen.findByRole('img', { name: 'Bhromon' });
    expect(mark).toHaveAttribute('src', '/api/v1/site/assets/logo?v=5');
  });

  it('falls back to the shipped name while the configuration is still loading', () => {
    stubApi([[/\/api\/v1\/site\/config/, { title: 'Not found' }, 404]]);

    renderScreen(<Logo />);

    // The header is never briefly blank: it reads Ghurify until the API answers.
    expect(screen.getByText('Ghurify')).toBeInTheDocument();
  });
});

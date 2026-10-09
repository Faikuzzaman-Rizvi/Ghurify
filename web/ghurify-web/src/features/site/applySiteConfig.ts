import type { SiteConfig, SiteThemeConfig } from './siteApi';

/**
 * Paints the site in the configured colours and type, and tells the browser what it is called.
 *
 * Everything here is a CSS variable or a tag in the document head, which is what makes a theme
 * change a setting rather than a release: the stylesheet's utilities are all written as
 * `var(--color-hill)`, so redefining that one variable recolours every button, badge and border
 * on every page at once.
 *
 * Called with the configuration from the API, and also with a draft while a super admin is
 * moving the colour pickers, which is what the live preview is.
 */
export function applyTheme(theme: SiteThemeConfig, root: HTMLElement = document.documentElement): void {
  for (const [token, value] of Object.entries(theme.colours)) {
    root.style.setProperty(`--color-${token}`, value);
  }

  root.style.setProperty('--font-sans', theme.bodyFontStack);
  root.style.setProperty('--font-display', theme.displayFontStack);

  loadFonts(theme.fontStylesheet);
}

/** Undoes {@link applyTheme}, so closing a preview falls back to what is actually saved. */
export function clearTheme(
  tokens: readonly string[],
  root: HTMLElement = document.documentElement,
): void {
  for (const token of tokens) {
    root.style.removeProperty(`--color-${token}`);
  }

  root.style.removeProperty('--font-sans');
  root.style.removeProperty('--font-display');
}

/**
 * The name, the description and the icons.
 *
 * The title and the icons are set here rather than in index.html because the site's name is not
 * known until the configuration has loaded. index.html still carries the shipped name and icon,
 * so the tab reads sensibly for the moment before this runs rather than being briefly blank.
 */
export function applyIdentity(config: SiteConfig, language: 'bn' | 'en'): void {
  const name = language === 'bn' ? config.identity.nameBn : config.identity.name;
  const description =
    language === 'bn' ? config.identity.descriptionBn : config.identity.description;

  document.title = name;

  setMeta('name', 'description', description);
  setMeta('name', 'theme-color', config.theme.colours['deep'] ?? '');

  // What a shared link shows. Set from the same values, so a renamed site does not keep
  // announcing its old name in chat apps.
  setMeta('property', 'og:site_name', name);
  setMeta('property', 'og:title', name);
  setMeta('property', 'og:description', description);

  const social = config.assets.find((asset) => asset.kind === 'social-image');
  if (social && social.url.length > 0) {
    setMeta('property', 'og:image', absolute(social.url));
  }

  const favicon = config.assets.find((asset) => asset.kind === 'favicon');
  if (favicon && favicon.isCustom) {
    // One uploaded icon replaces both of the shipped ones, whatever format it is in: a browser
    // picking between an SVG and a PNG that are no longer the same image would be worse.
    setIcon('icon', favicon.url);
  }

  const touch = config.assets.find((asset) => asset.kind === 'apple-touch-icon');
  if (touch && touch.isCustom) {
    setIcon('apple-touch-icon', touch.url);
  }
}

/** Adds or updates one meta tag, matched on whichever attribute names it. */
function setMeta(attribute: 'name' | 'property', key: string, content: string): void {
  if (content.length === 0) {
    return;
  }

  let tag = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);

  if (!tag) {
    tag = document.createElement('meta');
    tag.setAttribute(attribute, key);
    document.head.appendChild(tag);
  }

  tag.content = content;
}

/**
 * Points every icon link of one relation at the uploaded image. The shipped links are removed
 * rather than left alongside it, or the browser would be free to keep showing the old one.
 */
function setIcon(relation: 'icon' | 'apple-touch-icon', url: string): void {
  for (const existing of document.head.querySelectorAll(`link[rel="${relation}"]`)) {
    existing.remove();
  }

  const link = document.createElement('link');
  link.rel = relation;
  link.href = url;
  document.head.appendChild(link);
}

/**
 * Loads the stylesheet for the chosen font families, if it is not already loaded.
 *
 * The URL comes from the API, which builds it from a fixed list of families — the page never
 * constructs a third-party address from anything a person typed, and the content-security policy
 * allows only this one host.
 */
function loadFonts(href: string): void {
  if (href.length === 0) {
    return;
  }

  // Compared value by value rather than with an attribute selector: the URL carries ?, & and ;,
  // and escaping those for a selector is what made an earlier version add the stylesheet twice.
  for (const link of document.head.querySelectorAll('link[rel="stylesheet"]')) {
    if (link.getAttribute('href') === href) {
      return;
    }
  }

  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = href;
  // The shipped families are already loaded by index.html, so the page is never unstyled while
  // this arrives; a chosen family simply swaps in when it is ready.
  link.media = 'all';
  document.head.appendChild(link);
}

/** A same-origin path made absolute, because link previews need a full address. */
function absolute(url: string): string {
  return url.startsWith('http') ? url : new URL(url, window.location.origin).href;
}

import { beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * Each test gets a fresh copy of the module: it keeps the route registry and the "already asked
 * for this" set at module scope, so without this one test's registrations would leak into the next.
 */
async function freshModule() {
  vi.resetModules();
  return import('./routePrefetch');
}

function link(href: string, attributes: Record<string, string> = {}) {
  const anchor = document.createElement('a');
  anchor.href = href;
  for (const [name, value] of Object.entries(attributes)) {
    anchor.setAttribute(name, value);
  }
  document.body.append(anchor);
  return anchor;
}

beforeEach(() => {
  document.body.innerHTML = '';
});

describe('prefetchRoute', () => {
  it('fetches the code for the route that serves the path', async () => {
    const { registerRoute, prefetchRoute } = await freshModule();
    const load = vi.fn(() => Promise.resolve());

    registerRoute('/me/trips', load);
    prefetchRoute('/me/trips');

    expect(load).toHaveBeenCalledOnce();
  });

  it('matches a path with parameters in it', async () => {
    const { registerRoute, prefetchRoute } = await freshModule();
    const load = vi.fn(() => Promise.resolve());

    registerRoute('/admin/users/:id', load);
    prefetchRoute('/admin/users/42');

    expect(load).toHaveBeenCalledOnce();
  });

  it('leaves a path no route claims alone', async () => {
    const { registerRoute, prefetchRoute } = await freshModule();
    const load = vi.fn(() => Promise.resolve());

    registerRoute('/me/trips', load);
    prefetchRoute('/nothing/here');

    expect(load).not.toHaveBeenCalled();
  });

  it('does not match a child path against its parent', async () => {
    // "/admin" is the portal's own index. Were it to match "/admin/users" as well, hovering any
    // sidebar link would fetch the dashboard instead of the screen being pointed at.
    const { registerRoute, prefetchRoute } = await freshModule();
    const index = vi.fn(() => Promise.resolve());

    registerRoute('/admin', index);
    prefetchRoute('/admin/users');

    expect(index).not.toHaveBeenCalled();
  });

  it('asks for a given path only once', async () => {
    const { registerRoute, prefetchRoute } = await freshModule();
    const load = vi.fn(() => Promise.resolve());

    registerRoute('/me/trips', load);
    prefetchRoute('/me/trips');
    prefetchRoute('/me/trips');
    prefetchRoute('/me/trips');

    expect(load).toHaveBeenCalledOnce();
  });

  it('allows another attempt after a failed fetch', async () => {
    // A prefetch that fails (a dropped connection) must not poison the path: the reader may still
    // click it, and the real navigation has to be able to ask again.
    const { registerRoute, prefetchRoute } = await freshModule();
    const load = vi.fn(() => Promise.reject(new Error('offline')));

    registerRoute('/me/trips', load);
    prefetchRoute('/me/trips');
    await Promise.resolve();
    await Promise.resolve();
    prefetchRoute('/me/trips');

    expect(load).toHaveBeenCalledTimes(2);
  });
});

describe('watchLinksForPrefetch', () => {
  it('fetches a link\'s code when it is pointed at', async () => {
    const { registerRoute, watchLinksForPrefetch } = await freshModule();
    const load = vi.fn(() => Promise.resolve());
    registerRoute('/feed', load);

    const stop = watchLinksForPrefetch();
    link('/feed').dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
    stop();

    expect(load).toHaveBeenCalledOnce();
  });

  it('fetches it when the keyboard reaches it', async () => {
    const { registerRoute, watchLinksForPrefetch } = await freshModule();
    const load = vi.fn(() => Promise.resolve());
    registerRoute('/feed', load);

    const stop = watchLinksForPrefetch();
    link('/feed').dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
    stop();

    expect(load).toHaveBeenCalledOnce();
  });

  it('works from something inside the link, not just the link itself', async () => {
    const { registerRoute, watchLinksForPrefetch } = await freshModule();
    const load = vi.fn(() => Promise.resolve());
    registerRoute('/feed', load);

    const anchor = link('/feed');
    const icon = document.createElement('span');
    anchor.append(icon);

    const stop = watchLinksForPrefetch();
    icon.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
    stop();

    expect(load).toHaveBeenCalledOnce();
  });

  it('ignores links that leave the site, download, or open elsewhere', async () => {
    const { registerRoute, watchLinksForPrefetch } = await freshModule();
    const load = vi.fn(() => Promise.resolve());
    registerRoute('/feed', load);

    const stop = watchLinksForPrefetch();
    link('https://example.com/feed').dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
    link('/feed', { download: '' }).dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
    link('/feed', { target: '_blank' }).dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
    stop();

    expect(load).not.toHaveBeenCalled();
  });

  it('stops listening when told to', async () => {
    const { registerRoute, watchLinksForPrefetch } = await freshModule();
    const load = vi.fn(() => Promise.resolve());
    registerRoute('/feed', load);

    watchLinksForPrefetch()();
    link('/feed').dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));

    expect(load).not.toHaveBeenCalled();
  });
});

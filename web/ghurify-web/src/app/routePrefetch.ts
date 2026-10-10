import { matchPath } from 'react-router';

/**
 * Fetches a screen's code before anybody asks for it.
 *
 * Every screen behind a sign-in, and the whole admin portal, is a separate chunk (see
 * `router.tsx`), which is what keeps the first visit small. The cost is that opening one does two
 * things in a row: download the chunk, *then* fetch its data. Two round trips where there used to
 * be one, and on a real network that is what "navigation is slow" feels like.
 *
 * So the chunk is fetched earlier: when a link to it is hovered, focused or touched, and for the
 * screens somebody is most likely to open next, once the browser is idle. By the time the click
 * lands the code is usually already there, and the navigation is just the data fetch.
 *
 * `import()` is idempotent — the bundler and the browser both cache the module — so calling a
 * loader twice costs nothing. `requested` only avoids the needless call.
 */

type Loader = () => Promise<unknown>;

interface Entry {
  /** The route pattern, e.g. "/admin/users/:id". */
  pattern: string;
  load: Loader;
}

const entries: Entry[] = [];
const requested = new Set<string>();

/**
 * Called by the route helpers in `router.tsx` as they build the route tree, so the registry and
 * the routes cannot drift: a screen is registered by the same call that defines it.
 */
export function registerRoute(pattern: string, load: Loader): void {
  entries.push({ pattern, load });
}

/** Fetches the code for whichever route serves `pathname`. Safe to call repeatedly. */
export function prefetchRoute(pathname: string): void {
  if (requested.has(pathname)) {
    return;
  }

  // The first pattern that matches, which is the order the routes are declared in — the same
  // order the router itself would resolve them.
  const entry = entries.find((candidate) => matchPath(candidate.pattern, pathname) !== null);

  if (!entry) {
    return;
  }

  requested.add(pathname);

  // A failed prefetch must stay silent: nothing is waiting on it, and the real navigation will
  // ask for the chunk again and show its own error if it genuinely cannot be fetched.
  void entry.load().catch(() => requested.delete(pathname));
}

/**
 * Watches the document for links the reader is about to follow and fetches their code.
 *
 * One listener on the document rather than props on every link: the links live in the header, the
 * footer, the admin sidebar, trip cards and half the screens, and this way none of them has to
 * know about prefetching. `pointerover` covers mouse and pen, `focusin` the keyboard, and
 * `touchstart` gives a phone the brief moment between touch and click.
 */
export function watchLinksForPrefetch(): () => void {
  const onCandidate = (event: Event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const anchor = target.closest('a[href]');
    if (!(anchor instanceof HTMLAnchorElement)) {
      return;
    }

    // Same-origin, ordinary links only: another site's code is not ours to fetch, and a download
    // or a new tab is not a navigation this router will handle.
    if (anchor.origin !== window.location.origin || anchor.hasAttribute('download') || anchor.target) {
      return;
    }

    prefetchRoute(anchor.pathname);
  };

  document.addEventListener('pointerover', onCandidate, { passive: true });
  document.addEventListener('focusin', onCandidate, { passive: true });
  document.addEventListener('touchstart', onCandidate, { passive: true });

  return () => {
    document.removeEventListener('pointerover', onCandidate);
    document.removeEventListener('focusin', onCandidate);
    document.removeEventListener('touchstart', onCandidate);
  };
}

/**
 * Fetches a few screens' code once the browser has nothing better to do.
 *
 * Deliberately short: these are the screens a signed-in traveller opens most, and the portal's
 * own front door for staff. Prefetching everything would spend a phone's data on screens its
 * owner may never open, which is the problem the splitting was there to solve.
 */
export function prefetchLikelyRoutes(paths: readonly string[]): void {
  const run = () => paths.forEach(prefetchRoute);

  // Safari still has no requestIdleCallback; a short timer is close enough for this. Called
  // through window so it keeps its own `this`.
  if (typeof window.requestIdleCallback === 'function') {
    window.requestIdleCallback(run, { timeout: 3000 });
  } else {
    window.setTimeout(run, 1500);
  }
}

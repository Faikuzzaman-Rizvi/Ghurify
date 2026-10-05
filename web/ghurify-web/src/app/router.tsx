import { useEffect } from 'react';
import { createBrowserRouter, Outlet, RouterProvider, useLocation } from 'react-router';
import { HomePage } from './HomePage';
import { NotFoundPage } from './NotFoundPage';
import { SiteFooter } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { GuidedTour } from '@/components/tour/GuidedTour';
import { hasSeenTour, useTourStore } from '@/components/tour/tourStore';
import { LoginPage } from '@/features/auth/LoginPage';
import { AccountPage } from '@/features/auth/AccountPage';
import { ProtectedRoute } from '@/features/auth/ProtectedRoute';
import { useSilentRefresh } from '@/features/auth/useSilentRefresh';
import { ExplorePage } from '@/features/trips/ExplorePage';
import { TripDetailPage } from '@/features/trips/TripDetailPage';

/**
 * Wraps every route: header, footer and the guided tour, and the session is restored from
 * the httpOnly refresh cookie once, on load, rather than per screen.
 */
function AppShell() {
  useSilentRefresh();
  useScrollOnNavigate();
  useFirstVisitTour();

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="flex-1">
        <Outlet />
      </main>
      <SiteFooter />
      <GuidedTour />
    </div>
  );
}

/**
 * New page: back to the top. A link with a #hash (the header's "Destinations"): scroll to
 * that section once it has rendered.
 */
function useScrollOnNavigate() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    if (!hash) {
      window.scrollTo({ top: 0 });
      return;
    }

    const timer = window.setTimeout(() => {
      document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: 'smooth' });
    }, 50);

    return () => window.clearTimeout(timer);
  }, [pathname, hash]);
}

/** Offers the tour once, on a first visit to the home page. */
function useFirstVisitTour() {
  const { pathname } = useLocation();
  const start = useTourStore((state) => state.start);

  useEffect(() => {
    if (pathname !== '/' || hasSeenTour()) {
      return;
    }

    // Give the page a moment to lay out, so the first spotlight lands on the right place.
    const timer = window.setTimeout(start, 1200);
    return () => window.clearTimeout(timer);
  }, [pathname, start]);
}

const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/trips', element: <ExplorePage /> },
      { path: '/trips/:id', element: <TripDetailPage /> },
      {
        path: '/destinations/:slug',
        // Loaded on demand: it is the only screen with a map, and Leaflet is most of the
        // bundle. Everyone else never downloads it.
        lazy: async () => ({
          Component: (await import('@/features/trips/DestinationPage')).DestinationPage,
        }),
      },
      { path: '/login', element: <LoginPage /> },
      {
        path: '/account',
        element: (
          <ProtectedRoute>
            <AccountPage />
          </ProtectedRoute>
        ),
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}

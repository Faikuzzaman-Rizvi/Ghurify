import { useEffect, type ReactElement } from 'react';
import { createBrowserRouter, Outlet, useLocation, type RouteObject } from 'react-router';
// The DOM build of the provider: it can render a navigation synchronously (flushSync), which
// signing out relies on to leave a protected page before it notices the session has ended.
import { RouterProvider } from 'react-router/dom';
import { HomePage } from './HomePage';
import { NotFoundPage } from './NotFoundPage';
import { SiteFooter } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { SiteBackdrop } from '@/components/ui/SiteBackdrop';
import { Toasts } from '@/components/ui/Toasts';
import { PageLoading } from '@/components/States';
import { GuidedTour } from '@/components/tour/GuidedTour';
import { useIsOverHero } from '@/components/ui/headerStore';
import { hasSeenTour, useTourStore } from '@/components/tour/tourStore';
import { ProtectedRoute } from '@/features/auth/ProtectedRoute';
import { RoleRoute } from '@/features/auth/RoleRoute';
import { isStaff } from '@/features/auth/profileApi';
import { permissions } from '@/features/admin/permissions';
import { PermissionRoute } from '@/features/admin/PermissionRoute';
import { useSilentRefresh } from '@/features/auth/useSilentRefresh';
import { useForgetOnSignOut } from '@/features/auth/useForgetOnSignOut';
import { useLiveNotifications } from '@/hooks/useNotifications';
import { useAuthStore } from '@/features/auth/authStore';
import { useMyProfile } from '@/features/auth/useProfile';
import {
  prefetchLikelyRoutes,
  registerRoute,
  watchLinksForPrefetch,
} from './routePrefetch';
import { useAppliedSiteConfig } from '@/features/site/useSiteConfig';
import { ExplorePage } from '@/features/trips/ExplorePage';
import { TripDetailPage } from '@/features/trips/TripDetailPage';
import type { Profile } from '@/features/auth/profileApi';

/**
 * The root of every route, the site's and the admin portal's alike. The session is restored from
 * the httpOnly refresh cookie once, on load, and the live notification connection opened once;
 * here rather than in each frame, so moving between the site and the portal does neither again.
 */
function RootLayout() {
  useSilentRefresh();
  useForgetOnSignOut();
  useScrollOnNavigate();
  useLiveNotifications();
  useRoutePrefetching();
  // The site's own name, colours and icons, applied to the document. Here rather than in each
  // frame, so moving between the site and the admin portal does not reapply them.
  useAppliedSiteConfig();
  return (
    <>
      <Outlet />
      {/* One place for the short confirmations, so the site and the portal share it. */}
      <Toasts />
    </>
  );
}

/** The public site's frame: header, footer and the guided tour. */
function SiteShell() {
  useFirstVisitTour();
  // Photo-led pages set their own spacing down to the footer; the rest get a margin.
  const overHero = useIsOverHero();

  return (
    <div className="relative isolate flex min-h-screen flex-col">
      <SiteBackdrop />
      <SiteHeader />
      <main className={`flex-1 ${overHero ? '' : 'pb-20'}`}>
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

/**
 * Fetches a screen's code before it is opened: when a link to it is hovered, focused or touched,
 * and — once the browser is idle — for the screens this reader is most likely to open next.
 *
 * Without this, opening a screen meant downloading its chunk and only then fetching its data,
 * two round trips in a row. See routePrefetch.ts.
 */
function useRoutePrefetching() {
  const status = useAuthStore((state) => state.status);
  const { data: profile } = useMyProfile();

  useEffect(watchLinksForPrefetch, []);

  useEffect(() => {
    if (status !== 'authenticated') {
      // Nobody is signed in: the sign-in screen is the one thing worth having ready.
      prefetchLikelyRoutes(['/login']);
      return;
    }

    // What a signed-in traveller reaches for, and the portal's front door for staff — whose
    // next click is almost always a portal screen, and who are on a desk rather than mobile data.
    prefetchLikelyRoutes(
      profile && isStaff(profile)
        ? ['/admin', '/me/trips', '/account']
        : ['/me/trips', '/account'],
    );
  }, [status, profile]);
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

// --- Routes that fetch their screen the first time somebody opens it ---------------------------
//
// Only what an anonymous visitor browses is in the first download: the home page, the trip
// search, a trip's own page and the two sign-in screens. Everything behind a sign-in, and the
// whole admin portal, arrives when it is first opened. A traveller who never opens the portal
// never downloads it, which is what it was costing everybody before.
//
// Each helper mirrors the guard the screen had when it was imported eagerly, so what a route
// allows has not changed: only when its code is fetched has.

/** A screen behind a sign-in. */
function guarded(path: string, load: () => Promise<ReactElement>): RouteObject {
  registerRoute(path, load);
  return {
    path,
    hydrateFallbackElement: <PageLoading />,
    lazy: async () => ({ element: <ProtectedRoute>{await load()}</ProtectedRoute> }),
  };
}

/** A screen behind a platform role. */
function byRole(
  path: string,
  allow: (profile: Profile) => boolean,
  load: () => Promise<ReactElement>,
): RouteObject {
  registerRoute(path, load);
  return {
    path,
    hydrateFallbackElement: <PageLoading />,
    lazy: async () => ({ element: <RoleRoute allow={allow}>{await load()}</RoleRoute> }),
  };
}

/** A screen behind one host role, which is most of the hosting side. */
const byHost = (path: string, load: () => Promise<ReactElement>) =>
  byRole(path, (profile) => profile.roles.includes('Host'), load);

/**
 * The portal's frame. Every screen inside it needs this too, so prefetching one without the
 * other would still leave a download in the way of the first portal navigation.
 */
const adminShell = () => import('@/features/admin/AdminLayout');

/** An admin-portal screen behind one permission. */
function byPermission(
  route: { path: string } | { index: true },
  needs: string,
  load: () => Promise<ReactElement>,
): RouteObject {
  // The frame and the screen together, because opening a portal screen needs both. The index
  // route has no path of its own: it answers at "/admin", registered with the layout below.
  const pattern = 'path' in route ? `/admin/${route.path}` : '/admin';
  registerRoute(pattern, () => Promise.all([adminShell(), load()]));

  return {
    ...route,
    hydrateFallbackElement: <PageLoading />,
    lazy: async () => ({ element: <PermissionRoute needs={needs}>{await load()}</PermissionRoute> }),
  };
}

/** A public screen with no guard. */
function open(path: string, load: () => Promise<ReactElement>): RouteObject {
  registerRoute(path, load);
  return {
    path,
    hydrateFallbackElement: <PageLoading />,
    lazy: async () => ({ element: await load() }),
  };
}

const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      {
        element: <SiteShell />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/trips', element: <ExplorePage /> },
          { path: '/trips/:id', element: <TripDetailPage /> },
          // Fetched on demand, like the rest: between them they pull React Hook Form and the Zod
          // schemas (38 kB compressed), which the home page and the trip search have no use for.
          open('/login', async () => {
            const { LoginPage } = await import('@/features/auth/LoginPage');
            return <LoginPage />;
          }),
          open('/register', async () => {
            const { RegisterPage } = await import('@/features/auth/RegisterPage');
            return <RegisterPage />;
          }),
          open('/credits', async () => {
            const { CreditsPage: Page } = await import('./CreditsPage');
            return <Page />;
          }),
          open('/forgot-password', async () => {
            const { ForgotPasswordPage } = await import('@/features/auth/ForgotPasswordPage');
            return <ForgotPasswordPage />;
          }),
          // Loaded on demand: it is the only public screen with a map, and Leaflet is most of the
          // bundle. Everyone else never downloads it. Opened directly, the page waits for that
          // download inside the site's frame; followed from a link, it is already there.
          open('/destinations/:slug', async () => {
            const { DestinationPage } = await import('@/features/trips/DestinationPage');
            return <DestinationPage />;
          }),
          guarded('/account', async () => {
            const { AccountPage } = await import('@/features/auth/AccountPage');
            return <AccountPage />;
          }),
          guarded('/account/verify', async () => {
            const { VerificationPage } = await import('@/features/auth/VerificationPage');
            return <VerificationPage />;
          }),
          byHost('/host/trips', async () => {
            const { HostTripsPage } = await import('@/features/trips/HostTripsPage');
            return <HostTripsPage />;
          }),
          byHost('/host/trips/new', async () => {
            const { TripWizardPage } = await import('@/features/trips/TripWizardPage');
            return <TripWizardPage />;
          }),
          byHost('/host/trips/:id/edit', async () => {
            const { TripWizardPage } = await import('@/features/trips/TripWizardPage');
            return <TripWizardPage />;
          }),
          byHost('/host/trips/:id/requests', async () => {
            const { ManageRequestsPage } = await import('@/features/bookings/ManageRequestsPage');
            return <ManageRequestsPage />;
          }),
          byHost('/host/payouts', async () => {
            const { HostPayoutsPage } = await import('@/features/payments/HostPayoutsPage');
            return <HostPayoutsPage />;
          }),
          byHost('/host/payments', async () => {
            const { ReceivedPaymentsPage } = await import(
              '@/features/payments/ReceivedPaymentsPage'
            );
            return <ReceivedPaymentsPage />;
          }),
          // Loaded on demand with Leaflet, like the destination page.
          guarded('/me/map', async () => {
            const { TravelMapPage } = await import('@/features/travel/TravelMapPage');
            return <TravelMapPage />;
          }),
          guarded('/me/trips', async () => {
            const { MyTripsPage } = await import('@/features/bookings/MyTripsPage');
            return <MyTripsPage />;
          }),
          guarded('/me/payments', async () => {
            const { PaymentHistoryPage } = await import('@/features/payments/PaymentHistoryPage');
            return <PaymentHistoryPage />;
          }),
          guarded('/me/payments/:id', async () => {
            const { PaymentReceiptPage } = await import('@/features/payments/PaymentReceiptPage');
            return <PaymentReceiptPage />;
          }),
          guarded('/bookings/:id/checkout', async () => {
            const { CheckoutPage } = await import('@/features/payments/CheckoutPage');
            return <CheckoutPage />;
          }),
          guarded('/payments/sandbox', async () => {
            const { SandboxPaymentPage } = await import('@/features/payments/SandboxPaymentPage');
            return <SandboxPaymentPage />;
          }),
          guarded('/payments/result', async () => {
            const { PaymentResultPage } = await import('@/features/payments/PaymentResultPage');
            return <PaymentResultPage />;
          }),
          guarded('/trips/:id/chat', async () => {
            const { ChatPage } = await import('@/features/chat/ChatPage');
            return <ChatPage />;
          }),
          guarded('/trips/:id/safety', async () => {
            const { TripSafetyPage } = await import('@/features/safety/TripSafetyPage');
            return <TripSafetyPage />;
          }),
          guarded('/trips/:id/review', async () => {
            const { ReviewPage } = await import('@/features/feed/ReviewPage');
            return <ReviewPage />;
          }),
          open('/feed', async () => {
            const { FeedPage } = await import('@/features/feed/FeedPage');
            return <FeedPage />;
          }),
          open('/users/:id', async () => {
            const { PublicProfilePage } = await import('@/features/feed/PublicProfilePage');
            return <PublicProfilePage />;
          }),
          { path: '*', element: <NotFoundPage /> },
        ],
      },
      {
        // The admin portal has its own frame (sidebar and top bar), not the public header and
        // footer. The frame itself is fetched with the first portal screen somebody opens.
        path: '/admin',
        hydrateFallbackElement: <PageLoading />,
        lazy: async () => {
          const { AdminLayout } = await adminShell();
          return {
            element: (
              <RoleRoute allow={(profile) => isStaff(profile)}>
                <AdminLayout />
              </RoleRoute>
            ),
          };
        },
        children: [
          byPermission({ index: true }, permissions.dashboardView, async () => {
            const { AdminDashboardPage } = await import('@/features/admin/AdminDashboardPage');
            return <AdminDashboardPage />;
          }),
          byPermission({ path: 'verifications' }, permissions.usersVerify, async () => {
            const { VerificationQueuePage } = await import(
              '@/features/admin/VerificationQueuePage'
            );
            return <VerificationQueuePage />;
          }),
          byPermission({ path: 'reports' }, permissions.moderationReportsView, async () => {
            const { ReportsQueuePage } = await import('@/features/admin/ReportsQueuePage');
            return <ReportsQueuePage />;
          }),
          byPermission({ path: 'stories' }, permissions.moderationContentManage, async () => {
            const { StoriesModerationPage } = await import(
              '@/features/admin/StoriesModerationPage'
            );
            return <StoriesModerationPage />;
          }),
          byPermission({ path: 'disputes' }, permissions.moderationDisputes, async () => {
            const { ReportsQueuePage } = await import('@/features/admin/ReportsQueuePage');
            return <ReportsQueuePage disputes />;
          }),
          byPermission({ path: 'destinations' }, permissions.safetyDestinationsStatus, async () => {
            const { DestinationAlertsPage } = await import('@/features/admin/DestinationAlertsPage');
            return <DestinationAlertsPage />;
          }),
          byPermission({ path: 'sos' }, permissions.safetySosView, async () => {
            const { SosBoardPage } = await import('@/features/admin/SosBoardPage');
            return <SosBoardPage />;
          }),
          byPermission({ path: 'payouts' }, permissions.payoutsView, async () => {
            const { PayoutQueuePage } = await import('@/features/admin/PayoutQueuePage');
            return <PayoutQueuePage />;
          }),
          byPermission({ path: 'users' }, permissions.usersView, async () => {
            const { UsersPage } = await import('@/features/admin/UsersPage');
            return <UsersPage />;
          }),
          byPermission({ path: 'users/:id' }, permissions.usersView, async () => {
            const { UserDetailPage } = await import('@/features/admin/UserDetailPage');
            return <UserDetailPage />;
          }),
          byPermission({ path: 'trips' }, permissions.tripsView, async () => {
            const { AdminTripsPage } = await import('@/features/admin/AdminTripsPage');
            return <AdminTripsPage />;
          }),
          byPermission({ path: 'bookings' }, permissions.bookingsView, async () => {
            const { BookingLookupPage } = await import('@/features/admin/BookingLookupPage');
            return <BookingLookupPage />;
          }),
          byPermission({ path: 'payments' }, permissions.paymentsView, async () => {
            const { AdminPaymentsPage } = await import('@/features/admin/AdminPaymentsPage');
            return <AdminPaymentsPage />;
          }),
          byPermission({ path: 'payments/:id' }, permissions.paymentsView, async () => {
            const { AdminPaymentDetailPage } = await import(
              '@/features/admin/AdminPaymentDetailPage'
            );
            return <AdminPaymentDetailPage />;
          }),
          byPermission({ path: 'emergency-points' }, permissions.safetyPointsManage, async () => {
            const { EmergencyPointsPage } = await import('@/features/admin/EmergencyPointsPage');
            return <EmergencyPointsPage />;
          }),
          byPermission({ path: 'branding' }, permissions.settingsBranding, async () => {
            const { BrandingPage } = await import('@/features/admin/BrandingPage');
            return <BrandingPage />;
          }),
          byPermission({ path: 'theme' }, permissions.settingsTheme, async () => {
            const { ThemePage } = await import('@/features/admin/ThemePage');
            return <ThemePage />;
          }),
          byPermission({ path: 'staff' }, permissions.staffView, async () => {
            const { StaffMembersPage } = await import('@/features/admin/StaffMembersPage');
            return <StaffMembersPage />;
          }),
          byPermission({ path: 'roles' }, permissions.staffView, async () => {
            const { StaffRolesPage } = await import('@/features/admin/StaffRolesPage');
            return <StaffRolesPage />;
          }),
          byPermission({ path: 'audit' }, permissions.auditView, async () => {
            const { AuditLogPage } = await import('@/features/admin/AuditLogPage');
            return <AuditLogPage />;
          }),
        ],
      },
    ],
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}

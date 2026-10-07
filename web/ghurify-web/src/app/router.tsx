import { useEffect } from 'react';
import { createBrowserRouter, Outlet, useLocation } from 'react-router';
// The DOM build of the provider: it can render a navigation synchronously (flushSync), which
// signing out relies on to leave a protected page before it notices the session has ended.
import { RouterProvider } from 'react-router/dom';
import { CreditsPage } from './CreditsPage';
import { HomePage } from './HomePage';
import { NotFoundPage } from './NotFoundPage';
import { SiteFooter } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { PageLoading } from '@/components/States';
import { GuidedTour } from '@/components/tour/GuidedTour';
import { useIsOverHero } from '@/components/ui/headerStore';
import { hasSeenTour, useTourStore } from '@/components/tour/tourStore';
import { LoginPage } from '@/features/auth/LoginPage';
import { RegisterPage } from '@/features/auth/RegisterPage';
import { ForgotPasswordPage } from '@/features/auth/ForgotPasswordPage';
import { AccountPage } from '@/features/auth/AccountPage';
import { ProtectedRoute } from '@/features/auth/ProtectedRoute';
import { RoleRoute } from '@/features/auth/RoleRoute';
import { VerificationPage } from '@/features/auth/VerificationPage';
import { isStaff } from '@/features/auth/profileApi';
import { AdminLayout } from '@/features/admin/AdminLayout';
import { VerificationQueuePage } from '@/features/admin/VerificationQueuePage';
import { AdminDashboardPage } from '@/features/admin/AdminDashboardPage';
import { DestinationAlertsPage } from '@/features/admin/DestinationAlertsPage';
import { PayoutQueuePage } from '@/features/admin/PayoutQueuePage';
import { ReportsQueuePage } from '@/features/admin/ReportsQueuePage';
import { SosBoardPage } from '@/features/admin/SosBoardPage';
import { UsersPage } from '@/features/admin/UsersPage';
import { UserDetailPage } from '@/features/admin/UserDetailPage';
import { AdminTripsPage } from '@/features/admin/AdminTripsPage';
import { BookingLookupPage } from '@/features/admin/BookingLookupPage';
import { AdminPaymentsPage } from '@/features/admin/AdminPaymentsPage';
import { AdminPaymentDetailPage } from '@/features/admin/AdminPaymentDetailPage';
import { EmergencyPointsPage } from '@/features/admin/EmergencyPointsPage';
import { AuditLogPage } from '@/features/admin/AuditLogPage';
import { StoriesModerationPage } from '@/features/admin/StoriesModerationPage';
import { TripSafetyPage } from '@/features/safety/TripSafetyPage';
import { useSilentRefresh } from '@/features/auth/useSilentRefresh';
import { useForgetOnSignOut } from '@/features/auth/useForgetOnSignOut';
import { useLiveNotifications } from '@/hooks/useNotifications';
import { ExplorePage } from '@/features/trips/ExplorePage';
import { TripDetailPage } from '@/features/trips/TripDetailPage';
import { HostTripsPage } from '@/features/trips/HostTripsPage';
import { ManageRequestsPage } from '@/features/bookings/ManageRequestsPage';
import { MyTripsPage } from '@/features/bookings/MyTripsPage';
import { CheckoutPage } from '@/features/payments/CheckoutPage';
import { HostPayoutsPage } from '@/features/payments/HostPayoutsPage';
import { PaymentHistoryPage } from '@/features/payments/PaymentHistoryPage';
import { PaymentReceiptPage } from '@/features/payments/PaymentReceiptPage';
import { ReceivedPaymentsPage } from '@/features/payments/ReceivedPaymentsPage';
import { ChatPage } from '@/features/chat/ChatPage';
import { FeedPage } from '@/features/feed/FeedPage';
import { PublicProfilePage } from '@/features/feed/PublicProfilePage';
import { ReviewPage } from '@/features/feed/ReviewPage';
import { PaymentResultPage } from '@/features/payments/PaymentResultPage';
import { SandboxPaymentPage } from '@/features/payments/SandboxPaymentPage';
import { TripWizardPage } from '@/features/trips/TripWizardPage';

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
  return <Outlet />;
}

/** The public site's frame: header, footer and the guided tour. */
function SiteShell() {
  useFirstVisitTour();
  // Photo-led pages set their own spacing down to the footer; the rest get a margin.
  const overHero = useIsOverHero();

  return (
    <div className="flex min-h-screen flex-col">
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
    element: <RootLayout />,
    children: [
      {
        element: <SiteShell />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/credits', element: <CreditsPage /> },
          { path: '/trips', element: <ExplorePage /> },
          { path: '/trips/:id', element: <TripDetailPage /> },
          {
            path: '/destinations/:slug',
            // Loaded on demand: it is the only screen with a map, and Leaflet is most of the
            // bundle. Everyone else never downloads it. Opened directly, the page waits for that
            // download inside the site's frame.
            hydrateFallbackElement: <PageLoading />,
            lazy: async () => ({
              Component: (await import('@/features/trips/DestinationPage')).DestinationPage,
            }),
          },
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
          { path: '/forgot-password', element: <ForgotPasswordPage /> },
          {
            path: '/account',
            element: (
              <ProtectedRoute>
                <AccountPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/account/verify',
            element: (
              <ProtectedRoute>
                <VerificationPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/host/trips',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <HostTripsPage />
              </RoleRoute>
            ),
          },
          {
            path: '/host/trips/new',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <TripWizardPage />
              </RoleRoute>
            ),
          },
          {
            path: '/host/trips/:id/edit',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <TripWizardPage />
              </RoleRoute>
            ),
          },
          {
            path: '/host/trips/:id/requests',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <ManageRequestsPage />
              </RoleRoute>
            ),
          },
          {
            path: '/me/trips',
            element: (
              <ProtectedRoute>
                <MyTripsPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/bookings/:id/checkout',
            element: (
              <ProtectedRoute>
                <CheckoutPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/payments/sandbox',
            element: (
              <ProtectedRoute>
                <SandboxPaymentPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/payments/result',
            element: (
              <ProtectedRoute>
                <PaymentResultPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/trips/:id/chat',
            element: (
              <ProtectedRoute>
                <ChatPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/host/payouts',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <HostPayoutsPage />
              </RoleRoute>
            ),
          },
          {
            path: '/host/payments',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Host')}>
                <ReceivedPaymentsPage />
              </RoleRoute>
            ),
          },
          {
            path: '/me/payments',
            element: (
              <ProtectedRoute>
                <PaymentHistoryPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/me/payments/:id',
            element: (
              <ProtectedRoute>
                <PaymentReceiptPage />
              </ProtectedRoute>
            ),
          },
          { path: '/feed', element: <FeedPage /> },
          { path: '/users/:id', element: <PublicProfilePage /> },
          {
            path: '/trips/:id/safety',
            element: (
              <ProtectedRoute>
                <TripSafetyPage />
              </ProtectedRoute>
            ),
          },
          {
            path: '/trips/:id/review',
            element: (
              <ProtectedRoute>
                <ReviewPage />
              </ProtectedRoute>
            ),
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
      {
        // The admin portal has its own frame (sidebar and top bar), not the public header and footer.
        path: '/admin',
        element: (
          <RoleRoute allow={(profile) => isStaff(profile.roles)}>
            <AdminLayout />
          </RoleRoute>
        ),
        children: [
          { index: true, element: <AdminDashboardPage /> },
          {
            path: 'verifications',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <VerificationQueuePage />
              </RoleRoute>
            ),
          },
          {
            path: 'reports',
            element: (
              <RoleRoute
                allow={(profile) => profile.roles.some((r) => r === 'Admin' || r === 'Moderator')}
              >
                <ReportsQueuePage />
              </RoleRoute>
            ),
          },
          {
            path: 'stories',
            element: (
              <RoleRoute
                allow={(profile) => profile.roles.some((r) => r === 'Admin' || r === 'Moderator')}
              >
                <StoriesModerationPage />
              </RoleRoute>
            ),
          },
          {
            path: 'disputes',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <ReportsQueuePage disputes />
              </RoleRoute>
            ),
          },
          {
            path: 'destinations',
            element: (
              <RoleRoute
                allow={(profile) => profile.roles.some((r) => r === 'Admin' || r === 'SafetyDesk')}
              >
                <DestinationAlertsPage />
              </RoleRoute>
            ),
          },
          {
            path: 'sos',
            element: (
              <RoleRoute
                allow={(profile) => profile.roles.some((r) => r === 'Admin' || r === 'SafetyDesk')}
              >
                <SosBoardPage />
              </RoleRoute>
            ),
          },
          {
            path: 'payouts',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <PayoutQueuePage />
              </RoleRoute>
            ),
          },
          {
            path: 'users',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <UsersPage />
              </RoleRoute>
            ),
          },
          {
            path: 'users/:id',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <UserDetailPage />
              </RoleRoute>
            ),
          },
          {
            path: 'trips',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <AdminTripsPage />
              </RoleRoute>
            ),
          },
          {
            path: 'bookings',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <BookingLookupPage />
              </RoleRoute>
            ),
          },
          {
            path: 'payments',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <AdminPaymentsPage />
              </RoleRoute>
            ),
          },
          {
            path: 'payments/:id',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <AdminPaymentDetailPage />
              </RoleRoute>
            ),
          },
          {
            path: 'emergency-points',
            element: (
              <RoleRoute
                allow={(profile) => profile.roles.some((r) => r === 'Admin' || r === 'SafetyDesk')}
              >
                <EmergencyPointsPage />
              </RoleRoute>
            ),
          },
          {
            path: 'audit',
            element: (
              <RoleRoute allow={(profile) => profile.roles.includes('Admin')}>
                <AuditLogPage />
              </RoleRoute>
            ),
          },
        ],
      },
    ],
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}

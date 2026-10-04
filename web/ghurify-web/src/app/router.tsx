import { createBrowserRouter, RouterProvider } from 'react-router';
import { HomePage } from './HomePage';

/**
 * Routes are added per feature as the sprints land. Protected, role-aware routes
 * arrive with the auth feature.
 */
const router = createBrowserRouter([
  {
    path: '/',
    element: <HomePage />,
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}

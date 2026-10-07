import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import {
  apiUrl,
  connectionString,
  mailDirectory,
  paymentsProvider,
  repoRoot,
  webPort,
  webUrl,
} from './e2e/settings.mjs';

/**
 * End-to-end tests against a real, isolated stack: run `npm run test:e2e`, which prepares a clean
 * database first (e2e/prepare.mjs). See e2e/README.md.
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: webUrl,
    locale: 'en-GB',
    timezoneId: 'Asia/Dhaka',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // 360px wide: the smallest phone the app promises to work on.
    { name: 'mobile', use: { ...devices['Pixel 5'], viewport: { width: 360, height: 780 } } },
  ],
  webServer: [
    {
      command: `dotnet run --project "${path.join(repoRoot, 'src', 'Ghurify.Api')}" -c Release --no-build --no-launch-profile --urls ${apiUrl}`,
      url: `${apiUrl}/openapi/v1.json`,
      // The API's log in the test output: the first place to look when a step fails.
      stdout: 'pipe',
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        // Never load the developer's .env: no real email, no development database.
        DotEnv__Enabled: 'false',
        Database__ConnectionString: connectionString,
        Email__UserName: '',
        Email__Password: '',
        Email__PickupDirectory: mailDirectory,
        Jobs__Enabled: 'false',
        Ekyc__Provider: 'fake',
        // The offline pretend gateway, or the SSLCommerz sandbox with E2E_PAYMENTS=sslcommerz.
        Payments__Provider: paymentsProvider,
        Payments__ApiBaseUrl: apiUrl,
        Payments__WebBaseUrl: webUrl,
        Storage__ConnectionString: 'UseDevelopmentStorage=true',
        Storage__Container: 'e2e-media',
        Storage__DocumentsContainer: 'e2e-identity-documents',
        Storage__AllowedOrigins__0: 'http://localhost:5173',
        Storage__AllowedOrigins__1: webUrl,
        Cors__AllowedOrigins__0: webUrl,
      },
    },
    {
      command: `npm run dev -- --port ${webPort} --strictPort`,
      url: webUrl,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { GHURIFY_API_URL: apiUrl },
    },
  ],
});

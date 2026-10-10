import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

// The API target can be overridden for local development or E2E tests.
const apiTarget = process.env.GHURIFY_API_URL ?? 'http://localhost:5199';

/**
 * Hosts allowed to reach the dev server, beyond localhost.
 *
 * A leading dot is Vite's own wildcard for "this domain and any subdomain", so a new ngrok URL
 * needs no edit here. GHURIFY_ALLOWED_HOSTS (comma-separated) adds anything else, which is how a
 * named tunnel or a machine's own hostname is let in without changing the file.
 */
const allowedHosts = [
  '.ngrok-free.dev',
  '.ngrok-free.app',
  '.ngrok.io',
  '.trycloudflare.com',
  '.localhost',
  ...(process.env.GHURIFY_ALLOWED_HOSTS?.split(',')
    .map((host) => host.trim())
    .filter(Boolean) ?? []),
];

// Both the dev server and `vite preview` bind every interface, so a phone on the same Wi-Fi can
// open the site at http://<this-machine-lan-ip>:5173. See scripts/serve-lan.ps1.
const host = {
  host: '0.0.0.0',
  allowedHosts,

  // Forward browser requests to the ASP.NET Core API, so the whole site is one origin: no CORS
  // to configure for each new LAN address or tunnel URL, and cookies stay first-party.
  proxy: {
    '/api': {
      target: apiTarget,
      changeOrigin: true,
    },
    '/hubs': {
      target: apiTarget,
      changeOrigin: true,
      ws: true,
    },
  },
} as const;

export default defineConfig({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },

  server: { ...host, port: 5173, strictPort: true },

  // `npm run preview` serves the production build, which is what a phone or a tunnel should see:
  // the real chunking, real compression and no dev-server transform on every request.
  preview: { ...host, port: 4173, strictPort: true },

  build: {
    // Hashed file names are cached for a year by the server, so a source map would only ever be
    // fetched by somebody who asked for it in devtools. Kept out of the deployed build instead:
    // it would publish the whole readable source.
    sourcemap: false,
    // Raised deliberately: the entry is ~300 kB and the warning at 500 kB is what the default
    // chunking already satisfies. A real regression shows up in the numbers in the README.
    chunkSizeWarningLimit: 600,

    rollupOptions: {
      output: {
        // Rolldown splits per module by default, which produced 75 requests for the first paint,
        // 42 of them under a kilobyte (one file per icon). Each one costs a round trip and
        // compresses to more than its own size. These rules keep the long-lived dependencies in
        // their own cacheable chunks and let everything smaller ride along with its importer.
        advancedChunks: {
          minSize: 20_000,
          groups: [
            // The framework: changes only on an upgrade, so it stays cached across releases.
            { name: 'react', test: /node_modules[/\\](react|react-dom|scheduler)[/\\]/ },
            { name: 'router', test: /node_modules[/\\]react-router/ },
            { name: 'query', test: /node_modules[/\\]@tanstack/ },
            { name: 'forms', test: /node_modules[/\\](react-hook-form|zod|@hookform)/ },
            { name: 'i18n', test: /node_modules[/\\](i18next|react-i18next)/ },
            // Each language's strings: Bangla loads with the app, English on request.
            { name: 'strings-bn', test: /src[/\\]i18n[/\\]bn\.json/ },
            { name: 'strings-en', test: /src[/\\]i18n[/\\]en\.json/ },
            // Only the map screens pull these, and only when opened.
            { name: 'leaflet', test: /node_modules[/\\](leaflet|react-leaflet|@react-leaflet)/ },
            { name: 'realtime', test: /node_modules[/\\]@microsoft[/\\]signalr/ },
            { name: 'icons', test: /node_modules[/\\]lucide-react/ },
          ],
        },
      },
    },
  },

  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: true,
    testTimeout: 20_000,
  },
});

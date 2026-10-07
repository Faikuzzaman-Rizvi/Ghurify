// defineConfig comes from vitest/config so the `test` block below is type-checked.
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

// The API the dev server forwards to. GHURIFY_API_URL lets the end-to-end run point at its own
// isolated API (see e2e/README.md) while everyday development keeps the default.
const apiTarget = process.env.GHURIFY_API_URL ?? 'http://localhost:5199';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    // Mirrors the "@/*" paths entry in tsconfig.app.json.
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // The browser calls /api/... on its own origin and Vite forwards it to the API,
    // so local development needs no CORS exception and no base-URL switching.
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
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: true,
    // Screens typed into with user-event are slow when every test file runs in parallel; 5 s
    // (the default) made them flaky, not wrong.
    testTimeout: 20_000,
  },
});

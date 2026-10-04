// defineConfig comes from vitest/config so the `test` block below is type-checked.
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

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
        target: 'http://localhost:5199',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5199',
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
  },
});

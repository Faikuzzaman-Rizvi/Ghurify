import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { i18nReady } from '@/i18n';
import '@/styles/index.css';
import { AppProviders } from '@/app/providers';
import { AppRouter } from '@/app/router';

const container = document.getElementById('root');

if (!container) {
  throw new Error('No #root element in index.html.');
}

// The active language's strings are fetched alongside the app's own code, and the first render
// waits for them. Rendering first would paint translation keys and replace them a frame later.
await i18nReady;

createRoot(container).render(
  <StrictMode>
    <AppProviders>
      <AppRouter />
    </AppProviders>
  </StrictMode>,
);

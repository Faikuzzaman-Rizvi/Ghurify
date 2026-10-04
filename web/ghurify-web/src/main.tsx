import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import '@/i18n';
import '@/styles/index.css';
import { AppProviders } from '@/app/providers';
import { AppRouter } from '@/app/router';

const container = document.getElementById('root');

if (!container) {
  throw new Error('No #root element in index.html.');
}

createRoot(container).render(
  <StrictMode>
    <AppProviders>
      <AppRouter />
    </AppProviders>
  </StrictMode>,
);

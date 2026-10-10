import { configure } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { i18nReady } from '@/i18n';

// The strings are loaded on demand now (one chunk per language), so the suite waits for them
// here, once, rather than every test having to. Without this, assertions that look for visible
// text would race the first language file and see translation keys.
await i18nReady;

// findBy* waits up to 1 s by default; under a full parallel run a screen can take longer to settle.
configure({ asyncUtilTimeout: 5_000 });

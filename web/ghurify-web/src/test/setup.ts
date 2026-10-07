import { configure } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import '@/i18n';

// findBy* waits up to 1 s by default; under a full parallel run a screen can take longer to settle.
configure({ asyncUtilTimeout: 5_000 });

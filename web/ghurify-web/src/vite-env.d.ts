/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Absolute API origin. Left unset in development so the Vite proxy handles /api. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

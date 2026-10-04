---
paths:
  - "web/**"
---

# Frontend rules (web/ghurify-web)

- React 19 + TypeScript strict + Vite. Function components and hooks only.
- Folders: `src/app` (router, providers), `src/features/{auth,trips,bookings,payments,feed,guides,
  chat,safety,admin}`, `src/components` (shared UI), `src/api` (generated client), `src/hooks`,
  `src/i18n` (`bn.json`, `en.json`), `src/styles`.
- API client is **generated** from the backend OpenAPI (`npm run gen:api`). Never hand-write
  request types. Call it through TanStack Query hooks in each feature (`useTrip`, `useCreateTrip`).
- Server state in TanStack Query; small global client state (user, theme, language) in Zustand.
- Forms: React Hook Form + Zod schemas that mirror the backend validators.
- Styling: Tailwind with design tokens from the plan (hill green `#245C43`, deep `#173F2E`,
  turmeric `#D99A12`, jamdani `#A3305C`, mist `#F1F5F2`). Mobile-first; test at 360px width.
- Bangla-first: every visible string goes through i18next with both `bn` and `en` keys. Use a font
  stack that renders Bangla (e.g. Hind Siliguri, Noto Sans Bengali).
- Money shown as `Tk 6,800` (en) / `৳৬,৮০০` (bn) via one formatter. Dates in `Asia/Dhaka`.
- Accessibility: labels on every input, visible focus, buttons are `<button>`, images have alt text.
- Protected routes by role; hide actions the user cannot perform, but never rely on the UI for security.
- Every screen has loading, empty and error states.

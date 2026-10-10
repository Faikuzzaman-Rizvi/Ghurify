# Ghurify web

React 19 + TypeScript + Vite. The traveller, host, guide and admin screens.

```bash
npm install
npm run dev          # http://localhost:5173, proxying /api and /hubs to the API on :5199
npm run build        # type-check then production build into dist/
npm run preview      # serve dist/ (the real chunking and compression), reachable on the LAN
npm run lint
npm run test
npm run gen:api      # regenerate src/api/schema.d.ts from the running API's OpenAPI document
```

`npm run gen:api` needs the API running (`dotnet run --project src/Ghurify.Api`). Request and
response types are generated, never hand-written.

## Showing the site to another device

`..\Serve-Site.ps1` (repository root) builds this app, hands it to the API and serves both from one
origin, so a phone on the same Wi-Fi — or, with `-Ngrok`, anyone with the public URL — gets the
whole site from a single address. One origin means no CORS list to maintain and a first-party
sign-in cookie. Use that rather than exposing the dev server.

## Loading cost

The first visit downloads only what an anonymous visitor reads: the home page, the trip search, a
trip's page, and the Bangla strings. Everything else arrives when it is first opened — every
screen behind a sign-in, the whole admin portal, Leaflet, the SignalR client, and the English
strings. Routes are split in [`src/app/router.tsx`](src/app/router.tsx) (`guarded`, `byRole`,
`byHost`, `byPermission`, `open`); the vendor groups are in
[`vite.config.ts`](vite.config.ts) under `build.rollupOptions.output.advancedChunks`.

Adding a page means adding it there the same way. A new eager `import` of a screen in `router.tsx`
puts it back into everybody's first download, which is what these helpers exist to prevent.

To check the first-load cost after a change:

```bash
npm run build
# the files index.html references are the first load; everything else is on demand
```

As of the last measurement that set is 44 files, 293 kB compressed.

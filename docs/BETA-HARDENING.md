# Beta hardening report (Prompt 10)

What was reviewed before the pilot, what was found, what was fixed, and what is still open.

## 1. Security review

Reviewed against `.claude/rules/backend.md`: authentication, ownership checks, rate limits,
secrets, logging of sensitive data, uploads, CORS, plus headers and session handling.

### Fixed

| Severity | Finding | Fix | Test |
| --- | --- | --- | --- |
| High | A suspended or closed account kept working for up to 15 minutes on most endpoints: only the access token's signature was checked. | The token is rejected at validation time unless the account is active (one indexed read per request, cached for the request). | `Suspending_SignsThePersonOut_AndStopsSignIn_WithAnAuditEntry` |
| High | Auth rate limit (10/min per IP) also covered the silent session refresh that runs on every page load, so people sharing an IP (mobile carrier NAT) or opening pages quickly were signed out. | Credential endpoints 20/min per IP; refresh, logout and whoami their own 120/min policy. Brute force is still stopped per address in the database. | E2E journey |
| High | Two refreshes at once with the same cookie (React StrictMode, two tabs) looked like token theft, and the API ended the whole session. | Refresh is single-flight in a tab and serialised across tabs with a Web Lock. | `refreshSession.test.ts` |
| High | The DbUp pre-script `002_RemoveAccountsWithoutEmail` failed on an empty database, which would have blocked the first production deploy. | Guarded on the table existing. | E2E prepare (fresh database) |
| Medium | No hardening headers on API responses. | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `CSP: default-src 'none'`. Web app headers in `staticwebapp.config.json` (CSP report-only first). | Suspension test checks `nosniff` |
| Medium | Development secrets (public, in the repository) could reach another environment unnoticed. | The API refuses to start outside Development with any of them. | — |
| Medium | Role changes were not audited. | `role.granted` / `role.revoked` audit entries. | Unit tests |
| Medium | Browser uploads to a fresh storage account failed: the container and its CORS rule were only created on the first server-side read. | Containers and CORS are prepared at startup. Locally, Azurite also needed `--skipApiVersionCheck`. | E2E journey (ID photos) |
| Low | A client disconnecting mid-query surfaced as a SQL error in the logs. | Any failure after the caller has gone is treated as a disconnect (499, debug log). | — |

### Checked and fine

- **Passwords**: PBKDF2-SHA256, 600,000 iterations, per-password salt, hashes upgraded on sign-in;
  NIST rules (10+ characters, common passwords refused); sign-in paused after 5 failures per
  address, keyed on a hash so it reveals nothing about which addresses exist.
- **No enumeration**: register, resend and forgot-password answer the same for any address; wrong
  email and wrong password are the same error.
- **Sessions**: 15-minute JWT, rotating refresh token in an httpOnly, path-scoped cookie, replay
  revokes the family; password change or reset ends every session.
- **Ownership**: every user-owned read and write is filtered by owner in SQL as well as checked in
  the use case (spot-checked: bookings, chat, check-ins, documents, avatars, payouts).
- **Uploads**: type sniffed from the bytes, size-limited, metadata (GPS) removed, private
  containers, short-lived links; ID photos in their own container, only admins see them, every
  viewing audited, deleted 30 days after the decision.
- **SQL**: everything parameterised; no SQL built from strings.
- **Payments**: signed, idempotent webhooks; unique references on refunds and ledger entries.
- **CORS**: explicit origins only. **Logs**: emails and phones masked; codes only in the
  development senders (guarded by tests). **XSS**: no `dangerouslySetInnerHTML`.

### Still open

| Severity | Item | Mitigation / next step |
| --- | --- | --- |
| Medium | The realtime hubs take the access token in the query string (a browser WebSocket limitation), so proxy access logs could record it. | Tokens live 15 minutes; Serilog does not log query strings. Configure Front Door/App Service logs to drop query strings (go-live checklist). |
| Medium | No SMS provider: emergency contacts are not texted on SOS. | The desk calls them; the SOS screen says the text was not sent. Choose a provider before launch. |
| Low | Registration takes slightly different time for new and existing addresses (different emails are sent). | Rate-limited; acceptable. |
| Low | Web CSP is report-only. | Switch to enforcing after a clean week on staging (go-live checklist). |

## 2. Performance

- **Indexes** reviewed for the hot queries: search (`IX_Trip_Status_StartDate` with the filter
  columns included; `IX_Trip_DestinationId_StartDate`), feed (`IX_Post_AuthorId_Id`,
  `IX_Post_DestinationId_Id`, the clustered key for the global feed), chat
  (`IX_ChatMessage_TripId_Id` and the pinned-message filtered index). Each query seeks; none was
  changed.
- **Load test**: `tests/load/search.js` (k6) ramps to 5,000 virtual users browsing and searching,
  with thresholds p95 < 800 ms for search and < 600 ms for a trip page, under 1% errors.
  **Not run here**: k6 is not installed on this machine, and a meaningful number needs a
  staging-sized database and API. Run it against staging with
  `RateLimits__GlobalPerMinute` raised (one load generator is one IP): **p95 is not yet known.**

## 3. End-to-end test

`web/ghurify-web/e2e/core-journey.e2e.ts` (Playwright) runs the whole journey in Chromium at
**360px**: sign up and confirm the email → complete the profile → verify with ID photos → find a
trip → ask to join → the host signs in and approves → pay in the sandbox → chat → review the host.
It runs against an isolated stack (`npm run test:e2e`; see `e2e/README.md`) and **passes**. Getting
it to pass found four of the bugs above.

## 4. Screens

- Bangla and English: every visible string goes through i18next; the two files have the same keys
  (checked by the merge script on every change); a scan for hard-coded English in the markup found none.
- Loading, empty and error states: every screen added in this round has all three; the earlier
  screens were checked when they were built.
- 360px: the E2E journey runs at 360px, covering sign-up, account, verification, trip search and
  page, requests, checkout, chat and review. The admin pages were not driven at 360px.

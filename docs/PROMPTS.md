# Ghurify: Claude Code prompts

Copy one prompt at a time into Claude Code, from the repository root.
CLAUDE.md and `.claude/rules/*.md` load automatically, so the prompts stay short and focused.

## How to run each prompt

1. Start in **plan mode** (Shift+Tab) so Claude explores and proposes before editing.
2. Read the plan. Correct anything wrong, then approve.
3. Let it implement. If it drifts, stop it (Esc) and redirect.
4. Check the final summary: build, tests and lint must be green.
5. Commit, then `/clear` before the next prompt to keep the context clean.

Every prompt ends with the same process block. Keep it; it is what makes the output reliable.

```
PROCESS
1. Explore: read CLAUDE.md, the relevant .claude/rules files and the existing code you will touch.
2. Plan: list files to create/change, schema objects, endpoints, screens and tests. Flag any
   ambiguity or rule conflict as a question. Wait for my approval.
3. Implement in vertical slices: SSDT schema -> repository -> handler -> endpoint -> React -> tests.
4. Verify: dotnet build, dotnet test (unit + integration), npm run lint && npm run test.
   Fix failures; do not skip or delete tests.
5. Summarize: what changed, how it was tested, open questions, suggested commit message.
```

---

## Prompt 0: Bootstrap the repository (Sprint 1, weeks 1-2)

```
Bootstrap the Ghurify repository exactly as described in CLAUDE.md. No features yet, only a
working skeleton that builds, tests and runs end to end.

Create:
- Ghurify.sln with Ghurify.Api, Ghurify.Application, Ghurify.Domain, Ghurify.Infrastructure,
  Ghurify.DatabaseUpdate, tests/Ghurify.UnitTests, tests/Ghurify.IntegrationTests.
  Enforce the dependency rule with project references. Nullable on, warnings as errors,
  Directory.Build.props for shared settings, .editorconfig, nuget.config with <clear/>.
- src/Database/Ghurify.Database: SDK-style SSDT project (Microsoft.Build.Sql) with schemas
  Main, Pay, Social, Safety; the TVP Main.IdList; table Main.User with the standard columns
  and a temporal history table; Script.PostDeployment.sql (insert-if-missing pattern, empty for now).
- Ghurify.DatabaseUpdate: DbUp console (dbup-sqlserver), embedded scripts from Scripts/{Year}
  and Scripts/Pre, "pre" argument runs only Pre scripts, .WithTransaction(), reads the
  connection string from config/env and fails fast if missing. One sample no-op script.
- Ghurify.Infrastructure: IDbConnectionFactory, Procedures.cs constants, Serilog setup.
- Ghurify.Api: health endpoint /api/v1/health that runs SELECT 1, ProblemDetails, OpenAPI,
  options validation on start, CORS for the web app, rate limiter registered.
- tests: DatabaseProjectFileTests (every .sql under the SSDT folder listed exactly once in the
  .sqlproj, and every listed file exists); an integration test that starts SQL Server in
  Testcontainers, deploys the dacpac with DacFx, runs DbUp, and calls the health endpoint.
- web/ghurify-web: Vite + React 19 + TS strict, Tailwind with the colour tokens from
  .claude/rules/frontend.md, React Router, TanStack Query, Zustand, i18next with bn/en,
  ESLint + Prettier, Vitest, a home page that calls /api/v1/health, npm script gen:api.
- docker-compose.yml: SQL Server 2022 (with full-text), Redis, Azurite.
- CI (GitHub Actions): build (incl. dacpac), unit tests, integration tests, fail if integration
  tests executed = 0, frontend lint/test/build. Upload the dacpac and DbUp console as artifacts.
- README.md: setup steps and the deploy order (DbUp pre -> dacpac -> DbUp data -> apps).

Acceptance: a fresh clone runs `docker compose up -d`, publishes the dacpac, runs DbUp,
starts the API and web app, and the home page shows "API healthy". CI is green.

PROCESS (see top of docs/PROMPTS.md)
```

---

## Prompt 1: Phone OTP sign-up and login (Sprint 2, weeks 3-4)

```
Implement authentication (feature folder: Identity).

Schema: Main.User (Phone E.164 unique, DisplayName, Gender, Status), Main.OtpCode
(hashed code, expiry 5 min, attempts), Main.RefreshToken (hashed, family id, revoked).
Backend: POST /api/v1/auth/otp (send OTP via ISmsSender; console fake in dev),
POST /api/v1/auth/verify (create user on first login, issue JWT 15 min + refresh token),
POST /api/v1/auth/refresh (rotation; reuse of a revoked token revokes the whole family),
POST /api/v1/auth/logout. Rate limit OTP: 3 per phone per 10 min, 5 wrong attempts locks.
Never log codes or tokens.
Frontend: phone entry (+880 prefix), OTP entry with resend timer, auth store in Zustand,
silent refresh, protected route component. Bangla + English text.
Tests: OTP expiry, attempt lockout, refresh rotation and reuse detection, unauthorized access.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 2: Profiles, roles and NID verification (Sprint 3, weeks 5-6)

```
Implement profiles, roles and identity verification (Identity).

Schema: Main.UserProfile, Main.UserRole, Main.Verification (Level: Phone, Nid, NidSelfie;
NidHash only, never the raw number; Status; ProviderRef).
Backend: GET/PUT /api/v1/me/profile, POST /api/v1/me/verification (starts e-KYC through an
IEkycProvider; fake provider in dev returns approved/rejected by test NID), webhook/callback to
update status, policies VerifiedTraveler and VerifiedHost. Badges derived from verification level.
Admin: GET verification queue, approve/reject with reason (AdminOnly).
Frontend: profile page, verification flow screens, badge component, admin verification queue.
Tests: NID never stored in plain text, policy checks, non-admin cannot approve.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 3: Create trips (Sprint 4, weeks 7-8)

```
Implement trip creation (Trips).

Schema: Main.Destination (Name, Slug unique, Location GEOGRAPHY, Status Open/Caution/Closed;
seed Sajek, Bandarban, Cox's Bazar, Saint Martin's, Sylhet, Sreemangal, Tanguar Haor,
Sundarbans, Kuakata, Rangamati in Script.PostDeployment.sql), Main.Trip (temporal; HostId,
DestinationId, StartDate, EndDate, MeetingPoint, Seats, PricePerPerson, GroupType
Open/WomenOnly/Students/Families, Status Draft/Published/Full/Cancelled/Completed),
Main.TripCostItem (Category Transport/Stay/Food/Fees/Guide/Buffer, Amount),
Main.ItineraryDay (DayNo, Title, Plan, Difficulty). Procedures AddTrip and SetTrip write trip +
cost items + itinerary in one transaction.
Rules: only VerifiedHost can publish; PricePerPerson must equal the sum of cost items;
WomenOnly trips only by verified women hosts; cannot publish to a Closed destination.
Backend: POST /api/v1/trips (draft), PUT /api/v1/trips/{id}, POST /api/v1/trips/{id}/publish,
GET /api/v1/trips/{id}, GET /api/v1/me/trips.
Frontend: 4-step trip wizard (basics, cost breakdown with live total, itinerary days, review),
save as draft, publish.
Tests: price/sum rule, host ownership, publish blocked for closed destination.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 4: Find trips (Sprint 5, weeks 9-10)

```
Implement trip discovery (Trips).

Procedure Main.QueryTrips: filters destination, date range, max price, group type, seats
available, verified hosts only; sort by start date or price; paging (offset + page size,
total count). Add the supporting indexes inline in the table files.
Backend: GET /api/v1/trips (search), GET /api/v1/destinations, GET /api/v1/destinations/{slug}
(info + current status + upcoming trips).
Frontend: explore page with filters (URL query string as the source of truth), trip cards,
trip details page (host, cost breakdown, itinerary, seats with group mix, safety plan, refund
rules), destination page with Leaflet map. Loading, empty and error states.
Tests: each filter, paging, women-only trips hidden from users who cannot join them.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 5: Join requests and seat holds (Sprint 6, weeks 11-12)

```
Implement joining a trip (Bookings).

Schema: Main.JoinRequest (TripId, UserId, Message, Status Pending/Approved/Declined/Expired/
Cancelled, unique active request per user+trip), Pay.Booking (temporal; TripId, UserId,
Amount, Status Held/Confirmed/Cancelled/Refunded, HoldExpiresAt).
Flow: traveler requests -> host approves (creates Booking in Held with a 30-minute hold and
reserves a seat atomically in a procedure) or declines. Hangfire job every minute releases
expired holds (idempotent). Seats can never go below zero (concurrency test).
Notifications: NotificationHub pushes "new request" to host and "approved, pay within 30 min"
to traveler; store each one in a Main.Notification table.
Frontend: request-to-join dialog, host "manage requests" page, my trips page with status.
Tests: double approval for the last seat (only one succeeds), hold expiry, host-only approval.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 6: Payments and escrow (Sprint 7, weeks 13-14) - high risk

```
Implement payments and escrow (Payments). Take extra care: plan in detail first and list
every failure mode you will handle before writing code.

Schema: Pay.Payment (BookingId, Provider, ProviderTxnId unique, Amount, Status, IdempotencyKey
unique), Pay.EscrowLedger (append-only: BookingId, EntryType Hold/Release/Refund, Amount,
Counterparty, CreatedAt), Pay.WebhookEvent (provider event id unique, payload, processed at).
Backend: IPaymentGateway with an SSLCommerz implementation (sandbox) and a fake for tests.
POST /api/v1/bookings/{id}/payments (idempotency key header, returns redirect URL),
POST /api/v1/payments/webhooks/{provider} (verify signature, store event, process once:
Booking -> Confirmed, ledger Hold, add traveler to trip chat).
Rules: amount comes from the server, never the client; booking must be Held and unexpired.
Frontend: checkout page (fee shown before paying), redirect, success/failure return pages.
Tests: success, failure, timeout, duplicate webhook, forged signature, amount mismatch,
ledger balance per booking.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 7: Group chat, payouts and refunds (Sprint 8, weeks 15-16)

```
Implement trip group chat and money release (Chat, Payments).

Chat: Social.ChatMessage, ChatHub (/hubs/chat) with membership check per trip, history with
paging, phone numbers and wallet numbers in messages detected and masked with a warning
before booking is confirmed, pinned host announcements.
Payouts: Pay.Payout; Hangfire jobs release 40% before departure and 60% after trip start
(percentages from config), each writing ledger Release entries; jobs are idempotent.
Refunds: rules engine for traveler cancel (by days before departure), host cancel (full refund),
destination closed (full refund); writes ledger Refund + gateway refund call.
Frontend: chat screen (real-time, reconnect, unread count), host payouts page, refund status.
Tests: non-member cannot join hub group, payout job run twice pays once, refund rules table.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 8: Stories, media and reviews (Sprint 9, weeks 17-18)

```
Implement the social layer (Social).

Schema: Social.Post, Social.Media, Social.Comment, Social.Like, Social.Follow, Social.Review
(two-way: traveler->host, host->traveler, traveler->guide; only attendees of completed trips).
Media: POST /api/v1/media/upload-url returns a short-lived SAS URL; client uploads directly to
Blob storage; Hangfire job resizes images, strips GPS metadata, converts video to a streamable
format, then marks media ready.
Feed: GET /api/v1/feed (followed users + destination stories, paged), POST/DELETE likes,
comments, follow/unfollow. Reviews update a host rating summary.
Frontend: story composer with photo/video upload progress, feed, profile with trips and
reviews, review form after trip end.
Tests: only attendees can review, one review per pair per trip, GPS stripped.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 9: Safety and admin (Sprint 10, weeks 19-20)

```
Implement safety tools and the admin panel (Safety, Admin).

Safety: Safety.SosEvent, Safety.CheckIn, Safety.DestinationAlert, Safety.Report, Safety.AuditLog.
POST /api/v1/trips/{id}/sos (live location to SafetyHub, notify emergency contacts by SMS,
show nearest police/hospital from seeded data). Scheduled check-ins with a job that flags
missed check-ins to the safety desk. Closure alerts: admin/safety desk sets destination
status; when Closed, a job pauses new bookings, notifies members of affected trips and
starts refunds through the Prompt 7 rules.
Admin (React, same app, AdminOnly/SafetyDesk routes): verification queue, reports
moderation, disputes, destination alerts, live SOS board, payout approvals, dashboard counts.
Every admin action writes Safety.AuditLog.
Tests: SOS reaches the safety hub, closure triggers pause + refunds once, audit entries written.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 10: Harden for beta (Sprint 11, weeks 21-22)

```
Prepare for the beta with pilot hosts. Do not add features.

1. Security review of the whole repo against .claude/rules/backend.md: auth, ownership checks,
   rate limits, secrets, logging of sensitive data, file upload checks, CORS. List findings by
   severity, then fix critical and high ones.
2. Performance: review stored procedures and indexes for the search, feed and chat queries;
   add a k6 script simulating 5,000 users searching; report p95 latency.
3. Playwright end-to-end test of the core journey: sign up -> find trip -> request -> host
   approves -> pay (sandbox) -> chat -> review.
4. Check every screen has Bangla text, loading/empty/error states and works at 360px.
Output a short report of what was found, fixed, and still open.

PROCESS (see top of docs/PROMPTS.md)
```

## Prompt 11: Production release (Sprint 12, weeks 23-24)

```
Set up production deployment and operations.

- CD pipeline with the fixed order: DbUp pre -> SqlPackage publish (deployment report first,
  BlockOnPossibleDataLoss=true, DropObjectsNotInSource=false) -> DbUp data -> jobs worker ->
  API -> web. Staging first, manual approval for production.
- Health checks (SQL, Redis, Blob), structured logs, error alerts, uptime check.
- Backups: confirm automated backups and write a restore runbook; test one restore on staging.
- docs/RUNBOOK.md: deploy, rollback, rotate secrets, handle a stuck payment, handle an SOS.
- Go-live checklist from the implementation plan, marked done or open with evidence.

PROCESS (see top of docs/PROMPTS.md)
```

---

## Reusable prompts

### New feature (any time)

```
Feature: <one sentence>
User story: As a <role>, I want <goal> so that <reason>.
Acceptance criteria:
- <testable rule 1>
- <testable rule 2>
Out of scope: <what not to build>

PROCESS (see top of docs/PROMPTS.md)
```

### Bug fix

```
Bug: <what happens> | Expected: <what should happen> | Steps: <how to reproduce>
First write a failing test that reproduces it, then fix the cause (not the symptom),
then show the test passing. Explain the root cause in two sentences.
```

### Schema change

```
Change: <column/table/index/procedure>
Follow .claude/rules/database.md exactly: edit the SSDT file (and the history table if temporal),
add new files to the .sqlproj, add a DbUp data script only if existing rows need backfilling,
use Scripts/Pre only for intentional data loss. Show me the dacpac deployment report diff.
```

### Code review before merge

```
Review the changes on this branch against CLAUDE.md and .claude/rules. Report, by severity:
security and ownership gaps, money/ledger risks, rule violations (SQL, naming, SSDT/DbUp),
missing tests, missing Bangla text. Do not edit code; list findings with file and line.
```

# Ghurify

Social trip-sharing platform for Bangladesh. A host posts a trip (dates, seats, cost breakdown,
itinerary); travelers request to join, pay into escrow, chat, travel, and review. Guides and local
partners can be booked. Safety (verification, SOS, closure alerts) is the core promise.

Design docs (read when a task needs product context): docs/Ghurify-Implementation-Plan.pdf,
docs/Ghurify-Project-Proposal.pdf
Sprint prompts: docs/PROMPTS.md

## Stack (do not substitute without asking)

- Backend: ASP.NET Core Web API, .NET 10, C# latest, nullable enabled, warnings as errors
- Data access: **Dapper + stored procedures**. No EF Core, no ORM, no EF migrations.
- Database: SQL Server 2022. **Schema in SSDT (dacpac). Data changes in DbUp.** See .claude/rules/database.md
- Auth: custom email-OTP sign-in (a six-digit code emailed over SMTP), JWT (15 min) + rotating
  refresh tokens. No ASP.NET Core Identity (its default store needs EF Core). Email is the
  sign-in identity; phone is optional on the profile, for SOS, chat masking and payouts.
- Real-time: SignalR. Background work: Hangfire (SQL Server storage, own `HangFire` schema).
- Validation: FluentValidation. Logging: Serilog. API docs: built-in OpenAPI.
- Cache: Redis. Files: Azure Blob Storage (Azurite locally). Email: SMTP via MailKit.
- Frontend: React 19 + TypeScript (strict) + Vite, React Router, TanStack Query, Zustand,
  Tailwind CSS, React Hook Form + Zod, i18next (bn + en), SignalR client, Leaflet.
- Tests: xUnit (backend), Testcontainers SQL Server for integration, Vitest + Testing Library,
  Playwright for end-to-end.

## Solution layout

```
Ghurify.sln
src/
  Ghurify.Api/              endpoints, SignalR hubs, middleware, composition root
  Ghurify.Application/      use cases per feature, DTOs, validators, repository interfaces
  Ghurify.Domain/           entities, enums, domain rules. References nothing.
  Ghurify.Infrastructure/   Dapper repositories, payment/SMS/e-KYC/storage clients, Hangfire jobs
  Database/Ghurify.Database/  SSDT project (Microsoft.Build.Sql) -> Ghurify.Database.dacpac
  Ghurify.DatabaseUpdate/   DbUp console, data-only scripts
tests/
  Ghurify.UnitTests/        no database
  Ghurify.IntegrationTests/ real SQL Server built from the dacpac
web/ghurify-web/            React app (traveler, host, guide, admin areas)
docker-compose.yml          sqlserver, redis, azurite
```

Dependency rule: Domain <- Application <- Infrastructure <- Api. Application never references
Infrastructure. Feature folders use the same names everywhere:
`Identity, Trips, Bookings, Payments, Marketplace, Social, Chat, Safety, Notifications, Admin`.

## Commands

```bash
docker compose up -d                                   # sqlserver, redis, azurite
dotnet build Ghurify.sln                               # builds the dacpac too
dotnet run --project src/Ghurify.DatabaseUpdate -- pre # DbUp pre-schema scripts
sqlpackage /Action:Publish /SourceFile:src/Database/Ghurify.Database/bin/Debug/Ghurify.Database.dacpac /TargetConnectionString:"$GHURIFY_DB" /p:BlockOnPossibleDataLoss=true
dotnet run --project src/Ghurify.DatabaseUpdate        # DbUp data scripts
dotnet run --project src/Ghurify.DatabaseUpdate -- demo # sample hosts/trips (never in a release)
.\Publish-Database.ps1 [-Demo]                         # all of the above, in order, target from .env
dotnet run --project src/Ghurify.Api
dotnet test tests/Ghurify.UnitTests
dotnet test tests/Ghurify.IntegrationTests             # needs Docker
cd web/ghurify-web && npm run dev | npm run lint | npm run test | npm run gen:api
```

Database deploy order is always: **DbUp pre -> dacpac publish -> DbUp data -> apps.**

## How to work in this repo

1. **Explore first.** Read the relevant feature folder and rules before writing code. Reuse what exists.
2. **Plan before coding.** For any task touching more than 3 files, write a short plan (files to
   create/change, schema changes, tests) and wait for approval.
3. **Vertical slices.** Build one feature end to end (schema -> repository -> use case -> endpoint ->
   React screen -> tests) before starting the next. Keep each slice reviewable.
4. **Verify.** Run build, tests and lint before saying a task is done. Paste the result summary.
   Never claim a test passed without running it.
5. **Ask, don't guess,** when a requirement is ambiguous, a package is missing, or a rule here
   conflicts with the task. Never silently change the stack.
6. **Small commits** with Conventional Commit messages (`feat(trips): ...`, `fix(payments): ...`).

## Non-negotiable rules

- Every query is parameterized. No SQL string concatenation anywhere.
- Never query or save inside a loop. Batch with TVPs (`Main.IdList`) or one set-based procedure.
- Every read and write of user-owned data checks ownership or role in the use case **and** filters
  by the owner id in SQL.
- Money is `DECIMAL(18,2)` BDT in SQL and `decimal` in C#. Never `float`/`double`.
- Store all times as UTC (`DATETIME2`); display in `Asia/Dhaka`.
- Email addresses stored lower-cased and trimmed; phone numbers in E.164 (`+8801XXXXXXXXX`).
- Queue/job payloads carry ids only; the job reloads everything else.
- Secrets never in the repo. `appsettings.json` holds non-secret defaults only; use the gitignored `.env` (template: `.env.example`)
  locally and Key Vault / App Service settings when deployed.
- Fail fast at startup on missing required config; degrade (and log) on optional config.
- User-facing text: sentence case, Bangla and English keys in i18n files, never hardcoded.

## Definition of done

- Builds with zero warnings; unit + integration tests pass; frontend lint + tests pass
- New schema objects are in the SSDT project and listed in the .sqlproj
- New endpoints appear in OpenAPI and the React client is regenerated (`npm run gen:api`)
- Authorization tested for at least one "not allowed" case
- Bangla and English strings added for every new UI text
- Short summary: what changed, how it was tested, anything left open

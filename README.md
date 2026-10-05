# Ghurify

Social trip-sharing platform for Bangladesh. A host posts a trip (dates, seats, cost breakdown,
itinerary); travellers request to join, pay into escrow, chat, travel and review. Guides and local
partners can be booked. Safety — verification, SOS, closure alerts — is the core promise.

React + ASP.NET Core + SQL Server. See [CLAUDE.md](CLAUDE.md) for the working rules and
[docs/PROMPTS.md](docs/PROMPTS.md) for the sprint plan.

---

## What is here today

| Sprint | Feature | State |
| --- | --- | --- |
| 1 | Bootstrap: solution, SSDT + DbUp, CI, health check | done |
| 2 | Email-OTP sign-in, JWT + rotating refresh cookie | done |
| 4–5 (read side) | Destinations (10 seeded, with safety status), trip search, trip page with cost breakdown and itinerary | done |
| 4 (write side) | Trip wizard: create, edit, publish | next, needs Sprint 3 host verification |

The web app has a travel-style landing page, an explore page with filters, trip and destination
pages (with a map), Bangla and English throughout, and a guided tour (**Take the tour** in the
header; it also starts on a first visit).

For a showcase, load the sample hosts and trips with `.\Publish-Database.ps1 -Demo`; see
[docs/DEMO-SCRIPT.md](docs/DEMO-SCRIPT.md).

## Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | 10.0.401+ | pinned in `global.json` |
| Node.js | 22+ | for the React app |
| Docker | any recent | SQL Server, Redis, Azurite, and the integration tests |
| SqlPackage | 170+ | only for publishing the dacpac by hand (`dotnet tool install -g microsoft.sqlpackage`) |

## Getting started

```bash
# 1. Start the local dependencies (SQL Server 2022, Redis, Azurite)
docker compose up -d

# 2. Build everything, including the dacpac
dotnet build Ghurify.sln

# 3. Create the database schema (see "Deploy order" below)
export GHURIFY_DB="Server=localhost,1433;Database=Ghurify;User Id=sa;Password=Ghurify_Local_Dev_1;TrustServerCertificate=True;Encrypt=False;"
dotnet run --project src/Ghurify.DatabaseUpdate -- pre
sqlpackage /Action:Publish \
  /SourceFile:src/Database/Ghurify.Database/bin/Debug/Ghurify.Database.dacpac \
  /TargetConnectionString:"$GHURIFY_DB" \
  /p:BlockOnPossibleDataLoss=true \
  /p:DropObjectsNotInSource=false
dotnet run --project src/Ghurify.DatabaseUpdate

# 4. Run the API (http://localhost:5199)
dotnet run --project src/Ghurify.Api

# 5. Run the web app (http://localhost:5173) in another terminal
cd web/ghurify-web && npm install && npm run dev
```

Open <http://localhost:5173>. The footer's system status should read **API healthy** / **এপিআই সচল**.

### Pointing at another database (shared server, your own instance)

Never edit a connection string into a committed file. Put local settings in `.env` at the
repository root (gitignored); the API (in Development), the DbUp console and
`Publish-Database.ps1` all read it. Restart the API after changing it.

```powershell
Copy-Item .env.example .env      # then fill in Database__ConnectionString and the Email__ lines
.\Publish-Database.ps1 -Demo     # DbUp pre -> dacpac -> DbUp data -> demo trips, in order
```

`Publish-Database.ps1` takes the target from `-ConnectionString`, then `GHURIFY_DB`, then
.env, then user-secrets, then the docker container, and prints only the server and database name.
`-DryRun` writes the deployment script without touching the database.

The dev server proxies `/api` and `/hubs` to the API on port 5199, so the browser only ever talks
to its own origin and local development needs no CORS exception.

> The local SQL Server password above is a throwaway for the docker-compose container. Real
> connection strings come from `.env` locally and Key Vault / App Service settings when
> deployed. `appsettings*.json` is never published (`CopyToPublishDirectory=Never`).

## Deploy order

Every environment, every release, in this order. Nothing else is supported.

| # | Step | Command |
| --- | --- | --- |
| 1 | **DbUp pre** — data changes that need the *old* schema | `dotnet run --project src/Ghurify.DatabaseUpdate -- pre` |
| 2 | **Publish the dacpac** — the only thing that changes schema | `sqlpackage /Action:Publish ... /p:BlockOnPossibleDataLoss=true /p:DropObjectsNotInSource=false` |
| 3 | **DbUp data** — backfills against the *new* schema | `dotnet run --project src/Ghurify.DatabaseUpdate` |
| 4 | **Deploy the apps** | jobs worker, then API, then React web |

Why the flags matter:

- `BlockOnPossibleDataLoss=true` stops a deploy that would silently drop rows. If a change really
  must discard data, that goes in `Scripts/Pre` at step 1 — deliberately, reviewed.
- `DropObjectsNotInSource=false` protects the Hangfire and DbUp tables, which live outside SSDT.

## Tests

```bash
dotnet test tests/Ghurify.UnitTests/Ghurify.UnitTests.csproj          # no database
dotnet test tests/Ghurify.IntegrationTests/Ghurify.IntegrationTests.csproj  # needs Docker
cd web/ghurify-web && npm run lint && npm run test && npm run build
```

Pass the `.csproj` path, not the directory: passing a directory makes the SDK resolve a different
target and report "Zero tests ran".

The integration suite builds a real database the same way a release does — Testcontainers starts
SQL Server 2022, DacFx publishes the dacpac, DbUp runs the data scripts — then exercises the API
through `WebApplicationFactory`. Nothing is faked.

`global.json` opts `dotnet test` into Microsoft.Testing.Platform, which xunit.v3 runs on. That
runner exits with code 5 when zero tests run, so a suite that silently matches nothing fails CI
instead of looking green.

## Repository layout

```
Ghurify.sln
src/
  Ghurify.Api/                endpoints, SignalR hubs, middleware, composition root
  Ghurify.Application/        use cases per feature, DTOs, validators, repository interfaces
  Ghurify.Domain/             entities, enums, domain rules. References nothing.
  Ghurify.Infrastructure/     Dapper repositories, gateways, Hangfire jobs
  Database/Ghurify.Database/  SSDT project -> Ghurify.Database.dacpac
  Ghurify.DatabaseUpdate/     DbUp console, data-only scripts
tests/
  Ghurify.UnitTests/          no database
  Ghurify.IntegrationTests/   real SQL Server built from the dacpac
web/ghurify-web/              React app
docker-compose.yml            sqlserver, redis, azurite
```

Dependency rule: **Domain <- Application <- Infrastructure <- Api.** Application never references
Infrastructure, and Domain references nothing at all — it has no package references by design.

## Database changes

Schema lives in SSDT; data moves live in DbUp. They never swap jobs.

| If you need to… | Put it in |
| --- | --- |
| Add or change a column | `{Schema}/Tables/{Table}.sql` (and `{Table}History.sql` if temporal) |
| Add or drop an index | the same table file, after a `GO` |
| Add a query with joins | a stored procedure: `Query`/`Get`/`Add`/`Set`/`Del` + name |
| Add lookup or seed rows | `Script.PostDeployment.sql`, insert-if-missing |
| Fix or move existing data | `Scripts/{Year}/NNN_Name.sql` |
| Remove data before a schema change | `Scripts/Pre/NNN_Name.sql` |

Never write `ALTER`. Edit the object's file and let the dacpac work out the difference.

**Every `.sql` file must be listed in `Ghurify.Database.sqlproj` exactly once.** Default globbing is
off, and `DatabaseProjectFileTests` fails the build if a file is missing, listed twice, or listed
but deleted — otherwise an unlisted file builds green and is simply absent in production.

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs three jobs on every push and PR:

- **backend** — build (including the dacpac), unit tests; uploads the dacpac and the published
  DbUp console as artifacts
- **integration** — the Testcontainers suite, and a check that fails if it executed 0 tests
- **frontend** — lint, formatting, Vitest, production build

## Conventions worth knowing

- Every query is parameterised. No SQL string concatenation, anywhere.
- Money is `DECIMAL(18,2)` in SQL and `decimal` in C#. Never `float`/`double`.
- Times are stored UTC (`DATETIME2`) and displayed in `Asia/Dhaka`.
- Phone numbers are stored E.164 (`+8801XXXXXXXXX`); `Main.User` enforces this with a CHECK
  constraint and a unique index.
- Every user-facing string goes through i18next with both `bn` and `en` keys.

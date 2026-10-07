# Ghurify runbook

For whoever is on call. Each section is a situation and what to do, in order.

Environments: **staging** and **production**, each an Azure resource group with:

| Piece | What | Notes |
| --- | --- | --- |
| API | App Service (Linux, .NET 10) | `Jobs__RunServer=false` |
| Jobs worker | App Service, same build as the API | `Jobs__RunServer=true`; exactly one instance |
| Database | Azure SQL | `HangFire` schema lives here too |
| Storage | Storage account | containers `media` and `identity-documents` (both private) |
| Web | Static Web App | `staticwebapp.config.json` sets its headers |
| Secrets | Key Vault | App Services read them as Key Vault references |

Health: `GET /api/v1/health` (database only, for the load balancer) and
`GET /api/v1/health/ready` (database, storage, email, jobs; for people and deploys).

---

## First-time setup

1. **Azure**: create the pieces above per environment. Give the API and worker a managed identity
   with `get` on the Key Vault's secrets.
2. **Key Vault secrets** (App Service settings reference them; names use `--` for `:`):
   `Database--ConnectionString`, `Identity--OtpPepper`, `Identity--JwtSigningKey`,
   `Verification--NidPepper`, `Verification--CallbackSecret` (each 32+ random characters, different
   per environment), `Storage--ConnectionString`, `Email--UserName`, `Email--Password`,
   `Payments--SslCommerz--StoreId`, `Payments--SslCommerz--StorePassword`.
   Generate a pepper or key with `openssl rand -base64 48`.
3. **App settings** (not secret): `ASPNETCORE_ENVIRONMENT=Production`, `Payments__Provider=sslcommerz`,
   `Payments__ApiBaseUrl`, `Payments__WebBaseUrl`, `Cors__AllowedOrigins__0` (the web origin),
   `Storage__AllowedOrigins__0` (the web origin), `Jobs__RunServer` (see above),
   `Ekyc__Provider` (leave empty for manual review by admins).
   The API refuses to start with the fake payment gateway or a development secret in Production.
4. **Domains**: serve the API on a subdomain of the web app's domain (`api.ghurify.app` for
   `ghurify.app`). The sign-in cookie is `SameSite=Lax` and only travels same-site.
5. **GitHub**: environments `staging` and `production`; on `production`, add required reviewers
   (that is the manual approval). Per environment, secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
   `AZURE_SUBSCRIPTION_ID`, `DATABASE_CONNECTION_STRING`, `STATIC_WEB_APPS_TOKEN`, and variables
   `API_APP_NAME`, `JOBS_APP_NAME`, `RESOURCE_GROUP`, `API_BASE_URL`. In Azure, add a federated
   credential for the repository's environment to the deploy identity (OIDC, no stored passwords).
6. **First admin**: see "Make someone an admin" below.

## Deploy

Automatic: every green `main` deploys to staging; production waits for a reviewer to approve the
run in GitHub (Actions → Deploy → Review deployments). Both use the same build.

Each environment runs, in order: DbUp `pre` → schema deploy report (kept as an artifact) → dacpac
publish (`BlockOnPossibleDataLoss=true`, `DropObjectsNotInSource=false`) → DbUp data → jobs worker
→ API → readiness smoke test → web.

**Before approving production**: open the staging run's `schema-report-staging` artifact and read
it. Anything dropped or rebuilt that you did not expect: reject, and find out why.

If the publish stops on **possible data loss**, that is the guard working. Decide whether the loss
is intended; if it is, write a `Scripts/Pre` DbUp script that does it explicitly, then deploy again.

## Roll back

- **Web or API** (code only, no schema change): re-run the Deploy workflow on the previous good
  commit (Actions → Deploy → Run workflow, choose the commit). Or in the App Service, *Deployment
  Center → Logs → Redeploy* the previous build.
- **Schema**: never roll a dacpac back blindly. Schema changes are additive by rule; deploy the
  previous code (it ignores new columns). If a change must be undone, make it a new forward change.
- **Data script gone wrong**: restore Azure SQL to a point in time *as a new database*, compare,
  and copy back only what is needed. Never overwrite production with a restore.

## Rotate secrets

All in Key Vault; App Services pick up a new version on restart.

| Secret | Effect of rotating | How |
| --- | --- | --- |
| `Identity--JwtSigningKey` | Everyone's 15-minute access token stops working; they refresh silently. | New version → restart API and worker. |
| `Identity--OtpPepper` | Codes in flight stop working (sent again on request); the sign-in pause counters reset. | Same. |
| `Verification--NidPepper` | **Breaks "one ID, one account"**: old hashes no longer match. Only if leaked; then plan a re-verification. | Same, with care. |
| `Verification--CallbackSecret` | Must change at the e-KYC provider at the same moment. | Coordinate, then restart. |
| Payment, email, storage credentials | Rotate at the provider first, then update Key Vault and restart. | |

A leaked refresh token: suspend the account (admin portal → People → Suspend) or require a new
password; both end every session at once.

## A payment is stuck

Symptoms: a traveller paid, but the booking is still *held* or the seat was released.

1. Admin portal → **Bookings & payments**, search by the booking number or the payment reference
   the traveller quotes. You see every attempt, refund and what escrow holds.
2. Payment *Pending* for more than 30 minutes: the gateway's callback never arrived. Look the
   transaction up in the SSLCommerz merchant panel. If it succeeded there, ask the gateway to
   resend the IPN (the callback is idempotent; processing it twice is harmless). Do not edit the
   database by hand.
3. Paid after the seat was released: the API already refunded it automatically (reason *Paid after
   the hold ended*). Check the refund's status on the same page.
4. A refund *Failed*: fix the cause (gateway down, credentials), then **Retry failed refunds now**
   on the same page. Retries run by themselves every 15 minutes too; each refund has a unique
   reference, so retrying never pays twice.

Escrow must balance per booking (held = released + refunded + still in escrow). The page shows
it; a negative "still in escrow" is a bug: escalate.

## Someone raised an SOS

The safety desk sees it live on **Admin → SOS board** (and the host is notified; the emergency
contact is texted when SMS is configured).

1. **Acknowledge** it, so the rest of the desk knows someone has it.
2. Call the traveller (number on the card), then the host. If there is no answer and the
   position is in a remote place, call **999** and give them the map link.
3. Use **Emergency points** for the nearest police station or hospital. Check their numbers:
   seeded ones are approximate until someone marks them checked.
4. **Resolve** it when the person is safe, with what happened. The traveller can also say "I'm safe".

Missed check-ins appear under the SOS board. Call the host first.

## Close a destination (flood, unrest)

Admin → **Destination alerts** → Change status → *Closed*, with a note in both languages. New
requests stop at once; a background job cancels upcoming trips there and refunds everyone in full.
It runs once per closure even if retried.

## Make someone an admin

The first admin has to be made in SQL; after that, admins grant roles in **Admin → People**.
The person must first create an account and confirm their email.

```sql
DECLARE @Email NVARCHAR (256) = N'person@example.com';   -- lower case

INSERT INTO [Main].[UserRole] ([UserId], [Role])
SELECT [u].[Id], 10                                       -- 10 = Admin, 9 = SafetyDesk, 8 = Moderator
FROM   [Main].[User] AS [u]
WHERE  [u].[Email] = @Email
  AND  [u].[Status] = 1
  AND  NOT EXISTS (SELECT 1 FROM [Main].[UserRole] AS [r]
                   WHERE [r].[UserId] = [u].[Id] AND [r].[Role] = 10 AND [r].[Archived] = 0);
```

Run it with an account that can write to the database, and note who and why in the team log.

## Identity documents

Uploaded ID photos are in the private `identity-documents` container and are only ever opened by
admins through 5-minute links (each viewing is in the audit log). A daily job deletes them 30 days
after the check is decided, and uploads never submitted after 7 days. If a person asks for their
documents to be deleted sooner, decide their pending check (approve or reject), then delete the
blobs under `u{userId}/` by hand and note it in the audit log.

## Useful queries

```sql
-- What did an admin do recently?
SELECT TOP (50) a.Created, u.DisplayName, a.Action, a.EntityType, a.EntityId, a.Note
FROM Safety.AuditLog a JOIN Main.[User] u ON u.Id = a.ActorId ORDER BY a.Id DESC;

-- Are background jobs running? (Hangfire dashboard is not exposed; look at the tables.)
SELECT TOP (20) StateName, CreatedAt, InvocationData FROM HangFire.Job ORDER BY Id DESC;
```

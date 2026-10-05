# Manual verification

How to check, by hand, everything built so far: **Sprint 1 (bootstrap)** and **Sprint 2 (email OTP
sign-in)**. Every step below was run and its expected result confirmed before this was written.

Allow about 20 minutes for the whole thing. Parts A and B are the quick confidence check; C to F
are where you actually exercise the product.

---

## Before you start

**Pick which database you are testing against.** The API reads its connection string from
`Database__ConnectionString` in the gitignored `.env` at the repository root, which overrides
`appsettings.Development.json`. Restart the API after changing it.

| Where | How to select it |
| --- | --- |
| `ras-x2` shared server (current) | leave `.env` as it is |
| Your local `RIZVI` | set `Database__ConnectionString="<RIZVI connection string>"` in `.env` |
| Docker container | leave `Database__ConnectionString=` empty (the committed default is the container) |

Start the supporting containers (Redis and Azurite are not used yet, but SQL Server is if you
chose the container):

```powershell
docker compose up -d
```

---

## A. The automated suites

These prove the build and the rules. Run them first; if any fail, stop and read the failure.

```powershell
dotnet build Ghurify.sln -c Release
dotnet test tests/Ghurify.UnitTests/Ghurify.UnitTests.csproj -c Release --no-build
dotnet test tests/Ghurify.IntegrationTests/Ghurify.IntegrationTests.csproj -c Release --no-build
cd web/ghurify-web ; npm run lint ; npm run test ; npm run build
```

Expected:

| Suite | Expected |
| --- | --- |
| Build | **0 errors, 0 warnings** |
| Unit | **129 passed** |
| Integration | **40 passed** (needs Docker: it starts its own SQL Server) |
| Frontend lint | no output |
| Frontend tests | **34 passed** |
| Frontend build | succeeds |

Pass the `.csproj` path, not the folder. Passing a folder makes the SDK resolve a different target
and report "Zero tests ran".

The integration suite does **not** use your development database. It starts a throwaway SQL Server
container, publishes the dacpac into it, runs DbUp, then throws it away. So running tests cannot
damage your data, and a passing run proves the schema deploys from nothing.

---

## B. Prove a test can actually fail

A green suite only means something if it can go red. Break something on purpose:

1. Create an empty file `src/Database/Ghurify.Database/Main/Views/Scratch.sql` and do **not** add
   it to `Ghurify.Database.sqlproj`.
2. Run the unit tests.

Expected: `EverySqlFileOnDisk_IsListedInTheProject` **fails** and names `Main\Views\Scratch.sql`.
That guard is what stops a `.sql` file building green locally and simply being absent in
production.

Delete the file; the tests go green again.

---

## C. The API, by hand

Start it:

```powershell
dotnet run --project src/Ghurify.Api
```

Open **`src/Ghurify.Api/Ghurify.Api.http`** in Visual Studio and click **Send Request** above each
block. It is numbered in the order to run, and says what each should return.

The one-time code is never in a response. With SMTP configured it arrives by email; without
it, Development writes it to the API console:

```
DEVELOPMENT EMAIL: the sign-in code for r****i@example.com is 123456.
```

That line appears **only** in Development with no mail account configured.
`OtpSenderRegistrationTests` fails the build if that sender could ever be used elsewhere.

### Sending real email (optional)

Without SMTP credentials the code goes to the API console, which is enough for every check in
this document. To have it actually arrive in an inbox, use a Gmail account with 2-step
verification turned on, create a 16-character **app password**, and put it in `.env` (which is
gitignored, so nothing lands in the repository):

```dotenv
Email__Host=smtp.gmail.com
Email__UserName=you@gmail.com
Email__Password=abcdefghijklmnop
Email__FromAddress=you@gmail.com
```

Restart the API. The console line disappears and the code is emailed instead; the log then
says only "Sign-in code emailed to r\*\*\*\*i@example.com", never the code itself. Gmail allows
roughly 500 messages a day, which is far beyond what development needs.

If mail does not arrive, check the spam folder first, then the API log: an SMTP failure is
logged in full with the host and port, and the request fails rather than pretending to succeed.

### Watch out for the per-IP limit while testing

Sign-in endpoints allow **10 requests per minute per IP**. Manual poking trips that easily, and a
`429` from it looks identical to the per-address limit. If you get an unexpected `429`, wait a minute
and carry on. This is why the checks below use a fresh address each time.

### What to confirm

| # | Do this | Expect | What it proves |
| --- | --- | --- | --- |
| 1 | `GET /api/v1/health` | `200`, both statuses `Healthy` | API is up and really queries SQL |
| 2 | `POST /auth/otp` with a valid address | `202` | code issued |
| 3 | `POST /auth/verify` with the right code | `200` | account created on first sign-in |
| 4 | Look at request 3's response **headers** | `Set-Cookie: ghurify_rt=...; path=/api/v1/auth; httponly` | refresh token is not readable by page scripts |
| 5 | Look at request 3's response **body** | access token present, **no refresh token** | the long-lived credential never reaches JavaScript |
| 6 | `GET /auth/whoami` with no token | `401` | endpoints are closed by default |
| 7 | `GET /auth/whoami` with the token | `200`, address shown as `r****i@example.com` | token works; full address never leaves the server |
| 8 | `GET /auth/whoami` with `Bearer not.a.real.jwt` | `401` | signature is actually checked |

### What should fail, and why that matters

| # | Do this | Expect | What it proves |
| --- | --- | --- | --- |
| 9 | `POST /auth/otp` with `not-an-email` | `400` | the address cannot possibly be valid |
| 10 | `POST /auth/verify` for an address that never requested a code | `401` | — |
| 11 | `POST /auth/verify` with a wrong code | `401`, **identical wording to #10** | responses cannot be used to discover which addresses are registered |
| 12 | Request a code 4 times for one address | `202, 202, 202, 429` | 3 per address per 10 min, counted in SQL so it survives restarts and extra servers |
| 13 | Wrong code 5 times, then the **correct** one | five `401`s, then `401` again | the code locks; a correct code cannot rescue it |

Check 13 is the important one. Confirmed output:

```
wrong 1: 401   wrong 2: 401   wrong 3: 401   wrong 4: 401   wrong 5: 401
REAL code now: 401        <-- locked
```

### Session lifetime

Run these in order with the same cookie:

| Do this | Expect |
| --- | --- |
| `POST /auth/refresh` | `200`, and a **new** `ghurify_rt` cookie |
| `POST /auth/refresh` again | `200` |
| `POST /auth/logout` | `204` |
| `POST /auth/refresh` after logging out | `401` |

Confirmed: `200 / 200 / 204 / 401`.

### Replay detection, the one worth seeing

This is the security property the whole rotation design exists for.

1. Sign in. Save the `ghurify_rt` value — call it **A**.
2. `POST /auth/refresh` with **A**. You get `200` and a new cookie, **B**.
3. `POST /auth/refresh` with **A** again, as a thief with a stolen copy would.

Expected: `401` — **and B stops working too**. Refresh with B and it is also `401`.

A refresh token works exactly once. A second use means a copy escaped, and since there is no way
to tell the thief from the real user, the whole session is revoked and both must sign in again.
You can see it in the database in section E.

---

## D. The web app

With the API still running, in another terminal:

```powershell
cd web/ghurify-web
npm run dev
```

Open <http://localhost:5173>. The browser only ever talks to its own origin; Vite forwards `/api`
to the API, so there is no CORS exception in development.

| Do this | Expect |
| --- | --- |
| Open the home page | footer status reads **API healthy**; header has a **Sign in** button |
| Switch the language toggle to **বাংলা** | the whole page switches, including **এপিআই সচল** |
| Reload the page | it stays in Bangla (the choice is remembered) |
| Click **Sign in**, type `not-an-email` | inline error, **and no request is sent** (check the Network tab) |
| Type your email address, click **Send code** | moves to the code step; **Send a new code** is disabled and counting down |
| Read the code from the API console, enter it | you land back on the home page, signed in |
| Header now | shows **Your account** instead of **Sign in** |
| Open **Your account** | masked address, name "Not set yet" |
| **Reload the page** | you stay signed in |
| Open DevTools → Application → Local Storage | **no token of any kind** |
| DevTools → Application → Cookies | `ghurify_rt` present, **HttpOnly ticked** |
| In the Console, type `document.cookie` | the refresh token is **not** in the output |
| Click **Sign out**, then reload | you are signed out and stay signed out |
| Visit `/account` directly while signed out | redirected to `/login` |
| Narrow the window to 360px | layout still works, nothing clipped |

Two of those deserve a note. **Staying signed in after reload** works because the httpOnly cookie
restores the session — no token is kept in storage a script could read. And **`document.cookie` not
showing the token** is the practical payoff: an XSS bug cannot steal a credential that stays valid
for a month.

---

## E. The database

Open **`docs/verify-identity.sql`** in SSMS against your Ghurify database and run it. If you are on
`ras-x2`, change the `USE [Ghurify]` at the top to `USE [Ghurify-Rizvi]`.

It prints seven sections. What to look for:

| Section | Expect |
| --- | --- |
| 1. Tables | 4 tables; `Main.User` is `SYSTEM_VERSIONED_TEMPORAL_TABLE`, `Main.UserHistory` is `HISTORY_TABLE` |
| 2. Procedures | 5, each with a note on why it exists |
| 3. Indexes and constraints | `UX_User_Email` unique; `CK_User_Email` lower-case rule present |
| 4. OTP codes | `HashBytes` = 32, and `HashPrefix` is clearly not six digits |
| 5. Refresh tokens | see below |
| 6. Accounts | email stored lower-cased; phone is NULL until the profile collects it |
| 7. DbUp journals | one Pre script, one data script |

Section 5 is where replay detection becomes visible:

```
FamilyId                              TokensIssued  StillLive
10136390-B50E-4ECB-A1AA-17CC8DD72F9B       2            0      <-- replay: session shut down
DE043AA5-B950-43F2-9A49-4D21AB70A7F5       1            1      <-- normal, still signed in
```

One family per sign-in. At most one token per family should be live. A family with several tokens
and **zero** live is a replay that was caught.

To clear test data (foreign key order matters):

```sql
DELETE FROM Main.RefreshToken;
DELETE FROM Main.OtpCode;
DELETE FROM Main.[User];
```

---

## F. The deploy order

The release order is fixed and the same everywhere:

```
DbUp pre  ->  dacpac publish  ->  DbUp data  ->  apps
```

Prove it works from nothing: pick a database name that does not exist, point `GHURIFY_DB` at it,
and run the three steps. Expected:

1. **DbUp pre** — "the database does not exist yet... Nothing to do." and exit code 0
2. **sqlpackage publish** — creates the database, 4 tables, 5 procedures, 7 indexes, 1 foreign key,
   2 check constraints
3. **DbUp data** — "1 data script(s) applied"

Run step 3 a second time: "0 data script(s) applied". Re-running a deploy must change nothing.

Always publish with `BlockOnPossibleDataLoss=true` and `DropObjectsNotInSource=false`. The first
refuses a deploy that would discard rows; the second stops the dacpac deleting the Hangfire and
DbUp tables, which live outside the SSDT project. Both are already set in
`Ghurify.Database.publish.xml`.

---

## What is NOT built yet

So you do not go looking for things that were never in scope:

- Profiles, roles, NID verification (Sprint 3)
- Trip creation (the wizard), bookings, payments, chat, safety, admin. Trip **search and pages** are built: see docs/DEMO-SCRIPT.md
- Phone number capture on the profile (Sprint 3). `Main.User.Phone` exists but is always NULL.
- Playwright end-to-end tests (Sprint 11)
- An interactive API UI. `/openapi/v1.json` serves the document; there is no Swagger page yet.

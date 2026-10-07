# End-to-end tests

One Playwright test walks the core journey in a real browser at phone width (360px), against the
real API and a real database:

sign up (one emailed code) → complete the profile → verify with ID photos → find a trip →
ask to join → the host approves → pay into escrow (sandbox) → chat → review the host.

## Run

```bash
docker compose up -d          # sqlserver and azurite, from the repository root
cd web/ghurify-web
npm run test:e2e
```

`npm run test:e2e` first runs `e2e/prepare.mjs`, which:

1. drops and recreates the **`GhurifyE2E`** database on the local SQL Server container and
   publishes the schema, data scripts and demo data into it (`Publish-Database.ps1 -Demo`);
2. empties `e2e/.mail`, where the API writes account emails during the run;
3. builds the API (Release).

Playwright then starts its own API on **5299** and web server on **5174**, so a development API
on 5199 and web server on 5173 can keep running.

## What it never touches

- Your development database: it uses `GhurifyE2E` only.
- Your `.env`: the API runs with `DotEnv__Enabled=false`, no SMTP account, and writes the sign-up
  and reset codes to `e2e/.mail` instead (`PickupDirectoryOtpSender`, Development only).
- Your development blobs: uploads go to the `e2e-media` and `e2e-identity-documents` containers.

To remove everything it created:

```bash
docker exec ghurify-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Ghurify_Local_Dev_1 -C -Q "DROP DATABASE GhurifyE2E"
```

## Settings

`e2e/settings.mjs` holds the ports, database name and container name. Override with
`E2E_SQL_CONTAINER`, `E2E_SA_PASSWORD` or `E2E_DB` (a full connection string).

On a failure, `playwright-report/` has the trace and a screenshot (`npx playwright show-report`).

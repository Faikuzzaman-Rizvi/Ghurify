---
paths:
  - "src/Database/**"
  - "src/Ghurify.DatabaseUpdate/**"
  - "src/Ghurify.Infrastructure/**/Repositories/**"
---

# Database rules (SSDT + DbUp)

Two tools, two jobs. **SSDT owns the schema. DbUp only moves or fixes data.**

## SSDT project: src/Database/Ghurify.Database

- SDK-style project (`Microsoft.Build.Sql`) so `dotnet build` produces the dacpac on any OS.
- Folders: `{Schema}/Tables`, `{Schema}/Views`, `{Schema}/Stored Procedures`, `{Schema}/Functions`,
  `{Schema}/User Defined Types`, `Security/`, `Script.PostDeployment.sql`.
- Schemas: `Main` (users, verification, destinations, trips, marketplace), `Pay` (bookings,
  payments, escrow ledger, payouts, refunds), `Social` (posts, media, reviews, follows, chat),
  `Safety` (alerts, SOS, check-ins, reports, audit log).
- **One object per file.** File name = object name: `Main/Tables/Trip.sql`.
- **Schema changes are edits to that file. Never write `ALTER` scripts.** The dacpac computes the diff.
- **Indexes go inline in the table file after a `GO`.** Deleting the index from the file drops it.
- **Temporal tables** (`SYSTEM_VERSIONING = ON`) for `Main.User`, `Main.Trip`, `Pay.Booking`.
  A new column goes in both `{Table}.sql` and `{Table}History.sql`.
- `Pay.EscrowLedger` is append-only: no UPDATE or DELETE procedures for it, ever.
- Seed/lookup rows go in `Script.PostDeployment.sql` as **insert-if-missing**, safe on every deploy.
- Every `.sql` file must be listed in `Ghurify.Database.sqlproj` exactly once. A unit test enforces
  this (`DatabaseProjectFileTests`). If you add a file, add it to the project.
- Hangfire and DbUp create their own tables outside SSDT. Publish with
  `DropObjectsNotInSource=false` so they are never dropped.

## Naming and standard columns

- `[Schema].[PascalCaseTable]`, singular table names (`Main.Trip`, `Pay.Booking`).
- `Id BIGINT IDENTITY (1,1) NOT NULL` primary key `PK_{Table}`.
- Standard columns on every table: `Archived BIT CONSTRAINT DF_{Table}_Archived DEFAULT ((0)) NOT NULL`,
  `Created DATETIME2 (0) DEFAULT (getutcdate()) NOT NULL`,
  `UpdatedOn DATETIME2 (7) DEFAULT (getutcdate()) NOT NULL`, `UpdatedId BIGINT NULL`.
- Constraint names: `FK_{Table}_{RefTable}[_{Column}]`, `IX_{Table}_{Purpose}`, `UX_{Table}_{Purpose}`,
  `DF_{Table}_{Column}`, `CK_{Table}_{Rule}`.
- Soft delete = `Archived = 1`. Hard deletes only in purge jobs.
- Money `DECIMAL (18, 2)`; locations `GEOGRAPHY`; text `NVARCHAR (n)` sized deliberately.

## Stored procedures

- Use a procedure for anything with joins, reporting, search with many filters, or set-based work.
  Simple single-table CRUD may use parameterized Dapper SQL inside the repository.
- Name by intent: `Query{X}` (many rows), `Get{X}` (one row), `Add{X}`, `Set{X}` (update), `Del{X}`.
- List parameters use the TVP `Main.IdList (Id BIGINT)`. Never pass comma-separated strings.
- A procedure that writes several tables owns its own `BEGIN TRAN ... COMMIT` with `XACT_ABORT ON`.
- Procedure names live as constants in `Ghurify.Infrastructure/Data/Procedures.cs`.

## DbUp console: src/Ghurify.DatabaseUpdate

- Data only. No `CREATE`, `ALTER` or `DROP` of schema objects here.
- Scripts are embedded resources: `Scripts/{Year}/NNN_PascalCaseName.sql` and `Scripts/Pre/NNN_Name.sql`.
- Schema-qualified, no `GO`, idempotent where possible, one transaction per script (`.WithTransaction()`).
- **Never edit a script after it has run in production.** Write a new one.
- `Scripts/Pre` runs before the dacpac publish (`-- pre` argument), for intentional data loss that
  `BlockOnPossibleDataLoss=true` would otherwise block.

## Repositories (Dapper)

- Interfaces in `Ghurify.Application/{Feature}/I{X}Repository.cs`; implementations in
  `Ghurify.Infrastructure/Repositories/{Feature}/{X}Repository.cs`.
- Open a connection per method via `IDbConnectionFactory`; `await using` it.
- Always pass `CancellationToken` (`CommandDefinition`).
- Map columns explicitly to records; no `SELECT *`.
- Filter by owner id (`HostId`, `UserId`) in SQL, even if the caller already checked.

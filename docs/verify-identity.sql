/*
    Ghurify - verify the Identity feature (Prompt 1)

    WHERE TO RUN THIS
    -----------------
    The Ghurify database lives in the docker-compose SQL Server container, NOT in a locally
    installed SQL Server instance. Connect SSMS like this:

        Server name     : localhost,1433        (the comma is required, not a colon)
        Authentication  : SQL Server Authentication
        Login           : sa
        Password        : Ghurify_Local_Dev_1
        Trust server certificate : ticked

    The container must be running first:  docker compose up -d

    Then: USE Ghurify, and run this script. Every section prints what it checked.
*/

USE [Ghurify];   -- on ras-x2 this is [Ghurify-Rizvi]
GO

SET NOCOUNT ON;
GO

PRINT '=== 1. Tables, and which ones keep history =========================================';
-- Main.User is temporal: every change to an account is kept automatically in Main.UserHistory.
SELECT s.name                  AS [Schema],
       t.name                  AS [Table],
       t.temporal_type_desc    AS [Temporal]
FROM   sys.tables  AS t
JOIN   sys.schemas AS s ON s.schema_id = t.schema_id
WHERE  s.name IN ('Main', 'Pay', 'Social', 'Safety')
ORDER  BY s.name, t.name;
GO

PRINT '';
PRINT '=== 2. Stored procedures ==========================================================';
-- Each of these exists because its work must be atomic. Doing the same thing in C# would
-- leave a race: two requests reading the same stale count, or a revoke committing without
-- its matching insert.
SELECT s.name + '.' + p.name AS [Procedure],
       CASE p.name
            WHEN 'AddOtpCode'                   THEN 'Rate limit + insert in one transaction'
            WHEN 'SetOtpCodeAttempted'          THEN 'Count a wrong guess and lock at the limit'
            WHEN 'GetOrAddUserByEmail'          THEN 'Create the account on first sign-in, atomically'
            WHEN 'SetRefreshTokenRotated'       THEN 'Revoke old + issue successor together'
            WHEN 'SetRefreshTokenFamilyRevoked' THEN 'Kill a whole session on replay or sign-out'
            ELSE ''
       END AS [Why it is a procedure]
FROM   sys.procedures AS p
JOIN   sys.schemas    AS s ON s.schema_id = p.schema_id
ORDER  BY 1;
GO

PRINT '';
PRINT '=== 3. Indexes and constraints that enforce the rules ==============================';
SELECT  OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) AS [Table],
        i.name                                                          AS [Index],
        CASE WHEN i.is_unique = 1 THEN 'UNIQUE' ELSE '' END             AS [Unique]
FROM    sys.indexes AS i
WHERE   OBJECT_SCHEMA_NAME(i.object_id) = 'Main'
  AND   i.name IS NOT NULL
ORDER   BY 1, 2;

-- CK_User_Email keeps addresses lower-cased so one inbox cannot own two accounts;
-- CK_User_PhoneE164 stops a number being stored as 01712345678 instead of +8801712345678.
SELECT  OBJECT_SCHEMA_NAME(parent_object_id) + '.' + OBJECT_NAME(parent_object_id) AS [Table],
        name        AS [Check constraint],
        definition  AS [Rule]
FROM    sys.check_constraints
ORDER   BY 1, 2;
GO

PRINT '';
PRINT '=== 4. One-time codes are stored hashed, never in plain text ===================';
-- CodeHash is a 32-byte HMAC-SHA256, keyed with a server-side pepper that is NOT in the
-- database and bound to the email address. Leaking this table does not leak working codes.
SELECT  Id,
        Email,
        DATALENGTH(CodeHash)                            AS [HashBytes],      -- always 32
        CONVERT(VARCHAR(20), LEFT(CodeHash, 6), 2)      AS [HashPrefix],     -- not digits
        Attempts,
        CASE WHEN ConsumedOn IS NULL THEN 'no'  ELSE 'yes' END AS [Used],
        CASE WHEN LockedOn   IS NULL THEN 'no'  ELSE 'yes' END AS [Locked],
        ExpiresOn,
        Created
FROM    Main.OtpCode
ORDER   BY Id DESC;
GO

PRINT '';
PRINT '=== 5. Refresh tokens: rotation and replay detection ===============================';
/*
    How to read this:
      - One FamilyId per sign-in.
      - Each refresh REVOKES the presented token (ReplacedByHash set) and inserts its
        successor in the same family, so at most one token per family should be LIVE.
      - If a spent token is presented again, that is a replay: the WHOLE family is revoked,
        so you will see a family where every row is revoked and the newest has no replacement.
*/
SELECT  Id,
        UserId,
        FamilyId,
        CASE WHEN RevokedOn IS NULL THEN 'LIVE' ELSE 'revoked' END AS [State],
        CASE WHEN ReplacedByHash IS NULL THEN '-' ELSE 'rotated'  END AS [Replaced by successor],
        DATALENGTH(TokenHash) AS [HashBytes],   -- 32: the token itself is never stored
        ExpiresOn,
        Created
FROM    Main.RefreshToken
ORDER   BY FamilyId, Id;

-- Summary per session. A family with 0 live tokens and more than one row was shut down,
-- which is what a detected replay looks like.
SELECT  FamilyId,
        COUNT(*)                                                AS [Tokens issued],
        SUM(CASE WHEN RevokedOn IS NULL THEN 1 ELSE 0 END)      AS [Still live]
FROM    Main.RefreshToken
GROUP   BY FamilyId
ORDER   BY 1;
GO

PRINT '';
PRINT '=== 6. Accounts ===================================================================';
-- Email is the sign-in identity, stored lower-cased. Phone is optional until the profile
-- collects it, and is stored in E.164 when it is.
SELECT  Id, Email, Phone, DisplayName, Gender, Status, Created
FROM    Main.[User]
ORDER   BY Id;

-- Temporal history. Empty until a row is updated; sign-up alone writes no history.
SELECT  Id, Email, Phone, DisplayName, Status, SysStartTime, SysEndTime
FROM    Main.UserHistory
ORDER   BY Id, SysStartTime;
GO

PRINT '';
PRINT '=== 7. Migration journals (DbUp) ==================================================';
-- Two journals on purpose: Pre scripts run before the dacpac, data scripts after.
SELECT 'SchemaVersionsPre' AS [Journal], ScriptName, Applied FROM dbo.SchemaVersionsPre
UNION ALL
SELECT 'SchemaVersions',               ScriptName, Applied FROM dbo.SchemaVersions
ORDER  BY 1, 2;
GO

PRINT '';
PRINT '=== Done ==========================================================================';
GO

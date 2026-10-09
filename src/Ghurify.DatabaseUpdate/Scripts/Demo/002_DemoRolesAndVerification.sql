-- Roles and identity checks for the demo people, so a demo shows verified hosts, the badges and
-- the admin desk. Runs only through the "demo" stage, never in a release.
--
-- The demo hosts get the Host role and an approved national ID + selfie check. Their "NID hash"
-- is a hash of their email, not of any real ID number. Two staff accounts are added here, and
-- 004_DemoStaffRoles.sql puts them on the admin desk; their password is set by
-- 003_DemoPasswords.sql.
--
-- Every insert is guarded, so running the script again adds nothing.

INSERT INTO [Main].[User] ([Email], [DisplayName], [Gender], [Status])
SELECT [s].[Email], [s].[DisplayName], [s].[Gender], 1
FROM
(
    VALUES
    (N'admin@demo.ghurify.app',  N'Ghurify Admin',       CAST(2 AS TINYINT)),
    (N'safety@demo.ghurify.app', N'Ghurify Safety Desk', CAST(1 AS TINYINT))
) AS [s] ([Email], [DisplayName], [Gender])
WHERE NOT EXISTS (SELECT 1 FROM [Main].[User] AS [u] WHERE [u].[Email] = [s].[Email]);

-- Platform roles only (2 = Host). What somebody may do on the admin desk is a staff role now,
-- granted by 004_DemoStaffRoles.sql.
INSERT INTO [Main].[UserRole] ([UserId], [Role])
SELECT [u].[Id], [r].[Role]
FROM
(
    VALUES
    (N'nadia.rahman@demo.ghurify.app',    CAST(2 AS TINYINT)),
    (N'tanvir.hasan@demo.ghurify.app',    CAST(2 AS TINYINT)),
    (N'farhana.akter@demo.ghurify.app',   CAST(2 AS TINYINT)),
    (N'rafiq.chowdhury@demo.ghurify.app', CAST(2 AS TINYINT))
) AS [r] ([Email], [Role])
JOIN [Main].[User] AS [u] ON [u].[Email] = [r].[Email]
WHERE NOT EXISTS (SELECT 1
                  FROM   [Main].[UserRole] AS [existing]
                  WHERE  [existing].[UserId] = [u].[Id]
                    AND  [existing].[Role] = [r].[Role]
                    AND  [existing].[Archived] = 0);

-- Level 3 = national ID + selfie, Status 2 = approved.
INSERT INTO [Main].[Verification] ([UserId], [Level], [Status], [NidHash], [Provider], [ProviderRef], [ReviewedOn])
SELECT [u].[Id], 3, 2, HASHBYTES('SHA2_256', [u].[Email]), 'fake', CONCAT('demo-', [u].[Id]), SYSUTCDATETIME()
FROM   [Main].[User] AS [u]
WHERE  [u].[Email] IN (N'nadia.rahman@demo.ghurify.app', N'tanvir.hasan@demo.ghurify.app',
                       N'farhana.akter@demo.ghurify.app', N'rafiq.chowdhury@demo.ghurify.app')
  AND  NOT EXISTS (SELECT 1
                   FROM   [Main].[Verification] AS [v]
                   WHERE  [v].[UserId] = [u].[Id]
                     AND  [v].[Status] = 2);

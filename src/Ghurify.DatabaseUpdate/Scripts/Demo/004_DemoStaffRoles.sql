-- Puts the demo staff accounts on the admin desk, now that being on it means holding a row in
-- [Main].[UserStaffRole] rather than a role in [Main].[UserRole]. Runs only through the "demo"
-- stage, never in a release.
--
--   admin@demo.ghurify.app  -> super-admin, so a demo can drive the whole portal, roles and
--                              permissions included.
--   safety@demo.ghurify.app -> safety-desk.
--
-- Both sign in with the shared demo password from 003_DemoPasswords.sql. That is fine for
-- throwaway demo data on a local machine and nowhere else: the account is a real super admin,
-- so never load demo data into an environment that holds anything you care about.
--
-- Guarded, so running the script again adds nothing.

INSERT INTO [Main].[UserStaffRole] ([UserId], [StaffRoleId])
SELECT [u].[Id], [r].[Id]
FROM
(
    VALUES
    (N'admin@demo.ghurify.app',  'super-admin'),
    (N'safety@demo.ghurify.app', 'safety-desk')
) AS [seed] ([Email], [RoleKey])
JOIN [Main].[User]      AS [u] ON [u].[Email] = [seed].[Email] AND [u].[Archived] = 0
JOIN [Main].[StaffRole] AS [r] ON [r].[Key] = [seed].[RoleKey] AND [r].[Archived] = 0
WHERE NOT EXISTS (SELECT 1
                  FROM   [Main].[UserStaffRole] AS [h]
                  WHERE  [h].[UserId] = [u].[Id]
                    AND  [h].[StaffRoleId] = [r].[Id]
                    AND  [h].[Archived] = 0);

-- Tidies the rows an earlier run of 002 left behind: on a database where the demo data was
-- loaded before admin roles moved, the staff roles (8 Moderator, 9 SafetyDesk, 10 Admin) are
-- still sitting in [Main].[UserRole], where they now mean nothing. The data-stage migration
-- clears these for real accounts; this does it for the demo ones.
UPDATE [r]
SET    [r].[Archived]  = 1,
       [r].[UpdatedOn] = SYSUTCDATETIME()
FROM   [Main].[UserRole] AS [r]
JOIN   [Main].[User]     AS [u] ON [u].[Id] = [r].[UserId]
WHERE  [u].[Email] LIKE '%@demo.ghurify.app'
  AND  [r].[Role] BETWEEN 8 AND 10
  AND  [r].[Archived] = 0;

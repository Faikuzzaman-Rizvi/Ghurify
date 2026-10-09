-- Moves the admin desk off [Main].[UserRole] and onto the staff roles that replaced it.
--
-- Before this release, being on the desk meant holding Moderator (8), SafetyDesk (9) or
-- Admin (10) in [Main].[UserRole], and what each could do was fixed in code. Now it means
-- holding a row in [Main].[UserStaffRole], and what each role may do is data a super admin
-- edits from the portal. [Main].[UserRole] keeps the platform roles only: host, guide,
-- creator, operator, partner.
--
-- Mapping:
--   Moderator  (8)  -> the "moderator" staff role
--   SafetyDesk (9)  -> the "safety-desk" staff role
--   Admin      (10) -> the "super-admin" staff role
--
-- Admin maps to super admin, not to the "admin" role, on purpose. An admin could already grant
-- the Admin role to anybody, which is complete control of the platform in everything but name,
-- so mapping them to the new "admin" role would quietly take authority away from people who
-- had it, and could leave a deployment with nobody able to put it back. Demote them from
-- Super admin -> People on the desk once the real super admins are settled.
--
-- Idempotent: it only inserts where no live row exists, and only archives rows it has moved.

-- 1. Put everybody on the desk into the matching staff role.
INSERT INTO [Main].[UserStaffRole] ([UserId], [StaffRoleId], [GrantedById], [UpdatedId])
SELECT   [r].[UserId], [s].[Id], [r].[GrantedById], [r].[GrantedById]
FROM     [Main].[UserRole] AS [r]
JOIN     (VALUES (8, 'moderator'), (9, 'safety-desk'), (10, 'super-admin'))
             AS [map] ([Role], [RoleKey]) ON [map].[Role] = [r].[Role]
JOIN     [Main].[StaffRole] AS [s] ON [s].[Key] = [map].[RoleKey] AND [s].[Archived] = 0
WHERE    [r].[Archived] = 0
  AND    NOT EXISTS (SELECT 1
                     FROM  [Main].[UserStaffRole] AS [h]
                     WHERE [h].[UserId] = [r].[UserId]
                       AND [h].[StaffRoleId] = [s].[Id]
                       AND [h].[Archived] = 0);

-- 2. Archive the old rows, now that the access they granted lives somewhere else. Only rows
--    whose replacement is in place, so a partial run can be repeated safely.
UPDATE [r]
SET    [r].[Archived]  = 1,
       [r].[UpdatedOn] = SYSUTCDATETIME()
FROM   [Main].[UserRole] AS [r]
JOIN   (VALUES (8, 'moderator'), (9, 'safety-desk'), (10, 'super-admin'))
           AS [map] ([Role], [RoleKey]) ON [map].[Role] = [r].[Role]
JOIN   [Main].[StaffRole] AS [s] ON [s].[Key] = [map].[RoleKey] AND [s].[Archived] = 0
WHERE  [r].[Archived] = 0
  AND  EXISTS (SELECT 1
               FROM  [Main].[UserStaffRole] AS [h]
               WHERE [h].[UserId] = [r].[UserId]
                 AND [h].[StaffRoleId] = [s].[Id]
                 AND [h].[Archived] = 0);

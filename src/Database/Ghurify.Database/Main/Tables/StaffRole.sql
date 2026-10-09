-- A job on the admin desk: a name and the set of permissions that go with it.
--
-- Roles are data, not code, so a super admin can invent "Payments desk" or "Content editor"
-- from the portal without a release. Four are seeded by Script.PostDeployment.sql
-- (super-admin, admin, moderator, safety-desk) and marked IsSystem: their permissions can be
-- edited, but they cannot be deleted, because an empty desk would lock everyone out.
--
-- IsSuperAdmin marks the one role that holds every permission implicitly, including the ones a
-- later release adds. It keeps no permission rows (there would be nothing to compare them
-- against) and the role editor refuses to change it.
CREATE TABLE [Main].[StaffRole]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    -- Stable key used in audit rows and in the seed script. ASCII and lowercase, like a slug,
    -- so it reads the same everywhere and never needs escaping.
    [Key]           VARCHAR (40)    NOT NULL,
    [Name]          NVARCHAR (60)   NOT NULL,
    [NameBn]        NVARCHAR (60)   NOT NULL,
    [Description]   NVARCHAR (300)  NULL,
    [DescriptionBn] NVARCHAR (300)  NULL,
    [IsSystem]      BIT             CONSTRAINT [DF_StaffRole_IsSystem] DEFAULT ((0)) NOT NULL,
    [IsSuperAdmin]  BIT             CONSTRAINT [DF_StaffRole_IsSuperAdmin] DEFAULT ((0)) NOT NULL,

    [Archived]      BIT             CONSTRAINT [DF_StaffRole_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_StaffRole_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_StaffRole_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_StaffRole] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_StaffRole_Key] CHECK ([Key] NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_CS_AS
                                         AND LEN([Key]) > 0),
    -- The super-admin role is a system role by definition: nothing may delete it.
    CONSTRAINT [CK_StaffRole_SuperAdminIsSystem] CHECK ([IsSuperAdmin] = 0 OR [IsSystem] = 1)
);
GO

-- One live role per key. Archived rows are history and may repeat a key.
CREATE UNIQUE NONCLUSTERED INDEX [UX_StaffRole_Key]
    ON [Main].[StaffRole] ([Key] ASC)
    WHERE [Archived] = 0;
GO

-- Exactly one role can be the super-admin role, so "holds every permission" has one meaning.
CREATE UNIQUE NONCLUSTERED INDEX [UX_StaffRole_SuperAdmin]
    ON [Main].[StaffRole] ([IsSuperAdmin] ASC)
    WHERE [IsSuperAdmin] = 1 AND [Archived] = 0;
GO

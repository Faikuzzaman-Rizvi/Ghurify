-- What one staff role may do. One row per permission, keyed by the permission's string key
-- (Ghurify.Domain.Identity.Permission), e.g. payouts.approve.
--
-- A string rather than a lookup table on purpose: the list of permissions is owned by the code
-- that enforces them, so a release adds a key and the database needs no change. The API refuses
-- to store a key this build does not know, and ignores any it does not recognise on read, so
-- rolling the API back can only ever narrow access, never widen it.
--
-- Revoking archives the row rather than deleting it, so the audit trail of a role's permissions
-- stays readable. Granting the same permission again revives the archived row.
CREATE TABLE [Main].[StaffRolePermission]
(
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [StaffRoleId]  BIGINT         NOT NULL,
    [Permission]   VARCHAR (40)   NOT NULL,

    [Archived]     BIT            CONSTRAINT [DF_StaffRolePermission_Archived] DEFAULT ((0)) NOT NULL,
    [Created]      DATETIME2 (0)  CONSTRAINT [DF_StaffRolePermission_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]    DATETIME2 (7)  CONSTRAINT [DF_StaffRolePermission_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]    BIGINT         NULL,

    CONSTRAINT [PK_StaffRolePermission] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_StaffRolePermission_StaffRole] FOREIGN KEY ([StaffRoleId])
        REFERENCES [Main].[StaffRole] ([Id]),
    CONSTRAINT [CK_StaffRolePermission_Permission] CHECK (LEN([Permission]) > 0)
);
GO

-- A role holds a permission once. Archived rows are history and may repeat.
CREATE UNIQUE NONCLUSTERED INDEX [UX_StaffRolePermission_RoleId_Permission]
    ON [Main].[StaffRolePermission] ([StaffRoleId] ASC, [Permission] ASC)
    WHERE [Archived] = 0;
GO

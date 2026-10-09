-- Who is on the admin desk. Separate from [Main].[UserRole], which holds what a person is on
-- the platform (host, guide, creator): those are self-service or granted and gate the public
-- features, while these decide what somebody may do to everyone else's data.
--
-- Always granted by a person, never self-service, and GrantedById records who. Taking somebody
-- off the desk archives the row, so the history of who had access when stays readable.
CREATE TABLE [Main].[UserStaffRole]
(
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]       BIGINT         NOT NULL,
    [StaffRoleId]  BIGINT         NOT NULL,
    -- NULL only for the first super admin, who is made by the console command before any
    -- admin exists to grant it.
    [GrantedById]  BIGINT         NULL,

    [Archived]     BIT            CONSTRAINT [DF_UserStaffRole_Archived] DEFAULT ((0)) NOT NULL,
    [Created]      DATETIME2 (0)  CONSTRAINT [DF_UserStaffRole_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]    DATETIME2 (7)  CONSTRAINT [DF_UserStaffRole_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]    BIGINT         NULL,

    CONSTRAINT [PK_UserStaffRole] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserStaffRole_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_UserStaffRole_User_GrantedById] FOREIGN KEY ([GrantedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_UserStaffRole_StaffRole] FOREIGN KEY ([StaffRoleId]) REFERENCES [Main].[StaffRole] ([Id])
);
GO

-- A person holds a role once. Archived rows (revoked access) are history and may repeat.
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserStaffRole_UserId_StaffRoleId]
    ON [Main].[UserStaffRole] ([UserId] ASC, [StaffRoleId] ASC)
    WHERE [Archived] = 0;
GO

-- Read on every authorized request by [Main].[GetUserAccess]: this index is what keeps that cheap.
CREATE NONCLUSTERED INDEX [IX_UserStaffRole_UserId]
    ON [Main].[UserStaffRole] ([UserId] ASC)
    INCLUDE ([StaffRoleId])
    WHERE [Archived] = 0;
GO

-- The staff list, and "who else holds this role?" when one is being deleted.
CREATE NONCLUSTERED INDEX [IX_UserStaffRole_StaffRoleId]
    ON [Main].[UserStaffRole] ([StaffRoleId] ASC)
    INCLUDE ([UserId])
    WHERE [Archived] = 0;
GO

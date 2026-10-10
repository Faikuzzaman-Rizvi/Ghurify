-- Roles a user holds beyond the implicit Traveler role every account has.
--
-- 2 Host, 3 Creator, 4 Celebrity, 5 Guide, 6 Operator, 7 Partner, 8 Moderator, 9 SafetyDesk,
-- 10 Admin. Host is self-service (a person chooses to host); staff roles are granted by an
-- admin, and GrantedById records who did it. Revoking a role archives the row.
CREATE TABLE [Main].[UserRole]
(
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]       BIGINT         NOT NULL,
    [Role]         TINYINT        NOT NULL,
    [GrantedById]  BIGINT         NULL,

    [Archived]     BIT            CONSTRAINT [DF_UserRole_Archived] DEFAULT ((0)) NOT NULL,
    [Created]      DATETIME2 (0)  CONSTRAINT [DF_UserRole_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]    DATETIME2 (7)  CONSTRAINT [DF_UserRole_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]    BIGINT         NULL,

    CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserRole_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_UserRole_User_GrantedById] FOREIGN KEY ([GrantedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_UserRole_Role] CHECK ([Role] >= 2 AND [Role] <= 10)
);
GO

-- A role is held once. Archived rows (revoked roles) are history and may repeat.
CREATE UNIQUE NONCLUSTERED INDEX [UX_UserRole_UserId_Role]
    ON [Main].[UserRole] ([UserId] ASC, [Role] ASC)
    WHERE [Archived] = 0;
GO

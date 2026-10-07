-- A person's password, as a slow salted hash. One row per account that has set a password.
--
-- Kept apart from [Main].[User] on purpose: that table is temporal, and every password change
-- would otherwise copy the old hash into [Main].[UserHistory] for ever.
--
-- Algorithm: 1 PBKDF2-HMAC-SHA256. Iterations is stored per row, so the cost can be raised
-- later and old hashes are upgraded the next time their owner signs in.
CREATE TABLE [Main].[UserCredential]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]        BIGINT          NOT NULL,
    [PasswordHash]  VARBINARY (64)  NOT NULL,
    [PasswordSalt]  VARBINARY (32)  NOT NULL,
    [Iterations]    INT             NOT NULL,
    [Algorithm]     TINYINT         CONSTRAINT [DF_UserCredential_Algorithm] DEFAULT ((1)) NOT NULL,
    [ChangedOn]     DATETIME2 (0)   CONSTRAINT [DF_UserCredential_ChangedOn] DEFAULT (getutcdate()) NOT NULL,
    -- Set by an admin: the next sign-in must go through "forgot password" first.
    [MustReset]     BIT             CONSTRAINT [DF_UserCredential_MustReset] DEFAULT ((0)) NOT NULL,

    [Archived]      BIT             CONSTRAINT [DF_UserCredential_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_UserCredential_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_UserCredential_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_UserCredential] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserCredential_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_UserCredential_Algorithm] CHECK ([Algorithm] = 1),
    CONSTRAINT [CK_UserCredential_Iterations] CHECK ([Iterations] >= 1000)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_UserCredential_UserId]
    ON [Main].[UserCredential] ([UserId] ASC);
GO

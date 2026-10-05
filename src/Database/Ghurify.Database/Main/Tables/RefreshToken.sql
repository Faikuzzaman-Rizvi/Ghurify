-- Rotating refresh tokens.
--
-- Only a SHA-256 of the token is stored, so the database never holds a usable credential.
-- Every token belongs to a family: one family per sign-in. Rotation revokes the old token
-- and issues a new one in the same family. If a token that was already revoked is presented
-- again, it has been stolen and replayed, so the whole family is revoked at once.
CREATE TABLE [Main].[RefreshToken]
(
    [Id]              BIGINT            IDENTITY (1, 1) NOT NULL,
    [UserId]          BIGINT            NOT NULL,
    [TokenHash]       VARBINARY (32)    NOT NULL,
    [FamilyId]        UNIQUEIDENTIFIER  NOT NULL,
    [ExpiresOn]       DATETIME2 (0)     NOT NULL,
    [RevokedOn]       DATETIME2 (0)     NULL,
    -- The token that replaced this one, for tracing a replay back through the chain.
    [ReplacedByHash]  VARBINARY (32)    NULL,

    [Archived]        BIT               CONSTRAINT [DF_RefreshToken_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)     CONSTRAINT [DF_RefreshToken_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)     CONSTRAINT [DF_RefreshToken_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT            NULL,

    CONSTRAINT [PK_RefreshToken] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RefreshToken_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id])
);
GO

-- A token hash identifies exactly one row; the lookup on every refresh goes through this.
CREATE UNIQUE NONCLUSTERED INDEX [UX_RefreshToken_TokenHash]
    ON [Main].[RefreshToken] ([TokenHash] ASC);
GO

-- Revoking a family on replay, and signing out, both sweep by family.
CREATE NONCLUSTERED INDEX [IX_RefreshToken_FamilyId]
    ON [Main].[RefreshToken] ([FamilyId] ASC)
    INCLUDE ([RevokedOn]);
GO

-- Finding every live session for a user.
CREATE NONCLUSTERED INDEX [IX_RefreshToken_UserId]
    ON [Main].[RefreshToken] ([UserId] ASC)
    INCLUDE ([RevokedOn], [ExpiresOn]);
GO

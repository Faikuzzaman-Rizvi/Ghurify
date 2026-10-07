-- Identity checks a user has asked for, and what came of them.
--
-- Level: 1 Phone, 2 Nid, 3 NidSelfie. Status: 1 Pending, 2 Approved, 3 Rejected.
-- IdType: the document the check is based on: 1 National ID, 2 Passport, 3 Driving licence.
-- The document number itself is NEVER stored: only a keyed hash (HMAC-SHA256 with a server
-- secret), in the column still named NidHash. That is enough to stop one ID verifying two
-- accounts and useless to anyone who copies the table. ProviderRef is the e-KYC provider's
-- reference for the check, used to match its callback.
CREATE TABLE [Main].[Verification]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]        BIGINT          NOT NULL,
    [Level]         TINYINT         NOT NULL,
    [Status]        TINYINT         CONSTRAINT [DF_Verification_Status] DEFAULT ((1)) NOT NULL,
    [NidHash]       VARBINARY (32)  NULL,
    [Provider]      VARCHAR (30)    NOT NULL,
    [ProviderRef]   VARCHAR (100)   NULL,
    [IdType]        TINYINT         CONSTRAINT [DF_Verification_IdType] DEFAULT ((1)) NOT NULL,
    -- Why a check was rejected, in words the user is shown.
    [Reason]        NVARCHAR (300)  NULL,
    [ReviewedById]  BIGINT          NULL,
    [ReviewedOn]    DATETIME2 (0)   NULL,

    [Archived]      BIT             CONSTRAINT [DF_Verification_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_Verification_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_Verification_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_Verification] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Verification_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Verification_User_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Verification_Level] CHECK ([Level] BETWEEN 1 AND 3),
    CONSTRAINT [CK_Verification_Status] CHECK ([Status] BETWEEN 1 AND 3),
    CONSTRAINT [CK_Verification_IdType] CHECK ([IdType] BETWEEN 1 AND 3),
    -- NID checks always carry the hash; a phone check never does.
    CONSTRAINT [CK_Verification_NidHash] CHECK (([Level] = 1 AND [NidHash] IS NULL)
                                                OR ([Level] >= 2 AND [NidHash] IS NOT NULL))
);
GO

-- A user's checks, newest first: the profile badge and "is this person verified?" read this.
CREATE NONCLUSTERED INDEX [IX_Verification_UserId]
    ON [Main].[Verification] ([UserId] ASC, [Status] ASC)
    INCLUDE ([Level]);
GO

-- The admin queue: pending checks, oldest first.
CREATE NONCLUSTERED INDEX [IX_Verification_Status_Created]
    ON [Main].[Verification] ([Status] ASC, [Created] ASC)
    WHERE [Archived] = 0;
GO

-- One national ID verifies one account. Only approved checks count, so a rejected attempt or a
-- pending one never blocks the rightful owner.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Verification_NidHash_Approved]
    ON [Main].[Verification] ([NidHash] ASC, [Level] ASC)
    WHERE [Status] = 2 AND [Archived] = 0;
GO

-- The provider's callback finds its check by reference.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Verification_Provider_ProviderRef]
    ON [Main].[Verification] ([Provider] ASC, [ProviderRef] ASC)
    WHERE [ProviderRef] IS NOT NULL;
GO

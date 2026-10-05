-- One-time codes for email sign-in.
--
-- The code itself is never stored: only an HMAC-SHA256 of it, keyed with a server-side
-- pepper, so a database leak does not hand out working sign-in codes.
-- Not temporal: these rows are short-lived and a history of OTP hashes has no value.
CREATE TABLE [Main].[OtpCode]
(
    [Id]          BIGINT          IDENTITY (1, 1) NOT NULL,
    -- Stored lower-cased, matching [Main].[User].[Email], so a code issued for
    -- Rizvi@Example.com is found when the same person types rizvi@example.com.
    [Email]       NVARCHAR (256)  NOT NULL,
    [CodeHash]    VARBINARY (32)  NOT NULL,
    [ExpiresOn]   DATETIME2 (0)   NOT NULL,
    [Attempts]    TINYINT         CONSTRAINT [DF_OtpCode_Attempts] DEFAULT ((0)) NOT NULL,
    -- Set when the code is used successfully. A consumed code can never be used again.
    [ConsumedOn]  DATETIME2 (0)   NULL,
    -- Set when too many wrong guesses were made. The user must request a new code.
    [LockedOn]    DATETIME2 (0)   NULL,

    [Archived]    BIT             CONSTRAINT [DF_OtpCode_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)   CONSTRAINT [DF_OtpCode_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)   CONSTRAINT [DF_OtpCode_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT          NULL,

    CONSTRAINT [PK_OtpCode] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- The explicit collation matters: the database default is case-insensitive, under which
    -- [Email] = LOWER([Email]) is true for anything and enforces nothing.
    CONSTRAINT [CK_OtpCode_Email] CHECK ([Email] LIKE '%_@_%._%'
                                         AND [Email] = LOWER([Email]) COLLATE Latin1_General_CS_AS)
);
GO

-- Serves both reads on this table: counting codes sent to an address inside the rate
-- window, and finding the newest live code for that address.
CREATE NONCLUSTERED INDEX [IX_OtpCode_Email_Created]
    ON [Main].[OtpCode] ([Email] ASC, [Created] DESC)
    INCLUDE ([ExpiresOn], [ConsumedOn], [LockedOn], [Attempts]);
GO

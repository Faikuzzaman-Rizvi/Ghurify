-- The account record. One row per person, identified by email address.
--
-- Email is the sign-in identity. Phone is kept but optional: it is collected later on the
-- profile, because SOS alerts, chat number-masking and payouts all need a real number.
-- Temporal: every change is kept in [Main].[UserHistory].
CREATE TABLE [Main].[User]
(
    [Id]            BIGINT         IDENTITY (1, 1) NOT NULL,
    [Email]         NVARCHAR (256) NOT NULL,
    [Phone]         NVARCHAR (20)  NULL,
    [DisplayName]   NVARCHAR (100) NULL,
    -- 0 Unspecified, 1 Female, 2 Male, 3 Other. Nullable: nobody is forced to state it at
    -- sign-up, but women-only trips need it later.
    [Gender]        TINYINT        NULL,
    [Status]        TINYINT        CONSTRAINT [DF_User_Status] DEFAULT ((1)) NOT NULL,

    [Archived]      BIT            CONSTRAINT [DF_User_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)  CONSTRAINT [DF_User_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)  CONSTRAINT [DF_User_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT         NULL,

    [SysStartTime]  DATETIME2 (7)  GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [SysEndTime]    DATETIME2 (7)  GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,

    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- Stored lower-cased and trimmed, so one person cannot end up with two accounts, and so
    -- the OTP hash (which is keyed on the address) is computed over a stable value.
    -- The explicit collation is required: the database default is case-insensitive, under
    -- which [Email] = LOWER([Email]) is true for anything and enforces nothing at all.
    CONSTRAINT [CK_User_Email] CHECK ([Email] LIKE '%_@_%._%'
                                      AND [Email] = LOWER([Email]) COLLATE Latin1_General_CS_AS),
    -- Phone numbers are stored in E.164, e.g. +8801712345678.
    CONSTRAINT [CK_User_PhoneE164] CHECK ([Phone] IS NULL
                                          OR ([Phone] LIKE '+[0-9]%' AND LEN([Phone]) >= 8)),
    PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Main].[UserHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- One account per email address, including archived rows.
CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Email]
    ON [Main].[User] ([Email] ASC);
GO

-- Filtered: many accounts have no phone number yet, and NULLs must not collide with
-- each other. Once a number is given, it still belongs to exactly one account.
CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Phone]
    ON [Main].[User] ([Phone] ASC)
    WHERE [Phone] IS NOT NULL;
GO

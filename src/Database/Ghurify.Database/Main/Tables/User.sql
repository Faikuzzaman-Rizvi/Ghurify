-- The account record. One row per person, identified by phone number.
-- Temporal: every change is kept in [Main].[UserHistory].
CREATE TABLE [Main].[User]
(
    [Id]            BIGINT         IDENTITY (1, 1) NOT NULL,
    [Phone]         NVARCHAR (20)  NOT NULL,
    [DisplayName]   NVARCHAR (100) NULL,
    [Status]        TINYINT        CONSTRAINT [DF_User_Status] DEFAULT ((1)) NOT NULL,

    [Archived]      BIT            CONSTRAINT [DF_User_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)  CONSTRAINT [DF_User_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)  CONSTRAINT [DF_User_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT         NULL,

    [SysStartTime]  DATETIME2 (7)  GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [SysEndTime]    DATETIME2 (7)  GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,

    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- Phone numbers are stored in E.164, e.g. +8801712345678.
    CONSTRAINT [CK_User_PhoneE164] CHECK ([Phone] LIKE '+[0-9]%' AND LEN([Phone]) >= 8),
    PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Main].[UserHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- One account per phone number, including archived rows: a number must never be reused
-- while an old account still holds it.
CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Phone]
    ON [Main].[User] ([Phone] ASC);
GO

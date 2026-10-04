-- History table for [Main].[User]. SQL Server writes to it; nothing else does.
-- When a column is added to User.sql it must be added here too, in the same order.
CREATE TABLE [Main].[UserHistory]
(
    [Id]            BIGINT         NOT NULL,
    [Phone]         NVARCHAR (20)  NOT NULL,
    [DisplayName]   NVARCHAR (100) NULL,
    [Status]        TINYINT        NOT NULL,

    [Archived]      BIT            NOT NULL,
    [Created]       DATETIME2 (0)  NOT NULL,
    [UpdatedOn]     DATETIME2 (7)  NOT NULL,
    [UpdatedId]     BIGINT         NULL,

    [SysStartTime]  DATETIME2 (7)  NOT NULL,
    [SysEndTime]    DATETIME2 (7)  NOT NULL
);
GO

CREATE CLUSTERED INDEX [IX_UserHistory_Period]
    ON [Main].[UserHistory] ([SysEndTime] ASC, [SysStartTime] ASC);
GO

-- A safety check-in the host schedules for the group ("Arrived at Sajek", "Back at the cottage").
-- Anyone on the trip can mark it done. If the time passes (plus a grace period) without that, the
-- check-in job marks it Missed and the safety desk is alerted.
--
-- Status: 1 Scheduled, 2 Done, 3 Missed.
CREATE TABLE [Safety].[CheckIn]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]        BIGINT          NOT NULL,
    [Label]         NVARCHAR (150)  NOT NULL,
    [DueAt]         DATETIME2 (0)   NOT NULL,
    [Status]        TINYINT         CONSTRAINT [DF_CheckIn_Status] DEFAULT ((1)) NOT NULL,
    [CheckedInById] BIGINT          NULL,
    [CheckedInOn]   DATETIME2 (0)   NULL,
    [Note]          NVARCHAR (300)  NULL,

    [Archived]      BIT             CONSTRAINT [DF_CheckIn_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_CheckIn_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_CheckIn_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_CheckIn] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CheckIn_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_CheckIn_User_CheckedInById] FOREIGN KEY ([CheckedInById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_CheckIn_Status] CHECK ([Status] BETWEEN 1 AND 3)
);
GO

CREATE NONCLUSTERED INDEX [IX_CheckIn_TripId_DueAt]
    ON [Safety].[CheckIn] ([TripId] ASC, [DueAt] ASC);
GO

-- The missed check-in sweep looks at scheduled ones by due time.
CREATE NONCLUSTERED INDEX [IX_CheckIn_Scheduled_DueAt]
    ON [Safety].[CheckIn] ([DueAt] ASC)
    INCLUDE ([TripId])
    WHERE [Status] = 1 AND [Archived] = 0;
GO

-- Someone on a trip asking for help. Pushed live to the safety desk; their emergency contact is
-- texted. The last known position is updated while the SOS is open.
--
-- Status: 1 Open, 2 Acknowledged (the desk has picked it up), 3 Resolved.
-- Positions are WGS 84 latitude/longitude; Location holds the latest as GEOGRAPHY for distance queries.
CREATE TABLE [Safety].[SosEvent]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]          BIGINT          NOT NULL,
    [TripId]          BIGINT          NOT NULL,
    [Latitude]        DECIMAL (9, 6)  NOT NULL,
    [Longitude]       DECIMAL (9, 6)  NOT NULL,
    [AccuracyMeters]  INT             NULL,
    [Location]        GEOGRAPHY       NULL,
    [Message]         NVARCHAR (500)  NULL,
    [Status]          TINYINT         CONSTRAINT [DF_SosEvent_Status] DEFAULT ((1)) NOT NULL,
    [LastSeenOn]      DATETIME2 (0)   CONSTRAINT [DF_SosEvent_LastSeenOn] DEFAULT (getutcdate()) NOT NULL,
    [AcknowledgedById] BIGINT         NULL,
    [ResolvedById]    BIGINT          NULL,
    [ResolvedOn]      DATETIME2 (0)   NULL,

    [Archived]        BIT             CONSTRAINT [DF_SosEvent_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_SosEvent_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_SosEvent_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    CONSTRAINT [PK_SosEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SosEvent_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_SosEvent_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_SosEvent_User_AcknowledgedById] FOREIGN KEY ([AcknowledgedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_SosEvent_User_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_SosEvent_Status] CHECK ([Status] >= 1 AND [Status] <= 3),
    CONSTRAINT [CK_SosEvent_Position] CHECK ([Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)
);
GO

-- The live SOS board: everything not yet resolved, newest first.
CREATE NONCLUSTERED INDEX [IX_SosEvent_Status]
    ON [Safety].[SosEvent] ([Status] ASC, [Created] DESC)
    WHERE [Status] IN (1, 2) AND [Archived] = 0;
GO

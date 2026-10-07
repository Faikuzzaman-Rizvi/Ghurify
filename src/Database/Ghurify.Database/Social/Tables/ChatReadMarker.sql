-- How far each member has read a trip's chat, for the unread count. One row per member per trip.
CREATE TABLE [Social].[ChatReadMarker]
(
    [Id]          BIGINT         IDENTITY (1, 1) NOT NULL,
    [TripId]      BIGINT         NOT NULL,
    [UserId]      BIGINT         NOT NULL,
    [LastReadId]  BIGINT         NOT NULL,

    [Archived]    BIT            CONSTRAINT [DF_ChatReadMarker_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)  CONSTRAINT [DF_ChatReadMarker_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)  CONSTRAINT [DF_ChatReadMarker_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT         NULL,

    CONSTRAINT [PK_ChatReadMarker] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChatReadMarker_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_ChatReadMarker_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_ChatReadMarker_TripId_UserId]
    ON [Social].[ChatReadMarker] ([TripId] ASC, [UserId] ASC);
GO

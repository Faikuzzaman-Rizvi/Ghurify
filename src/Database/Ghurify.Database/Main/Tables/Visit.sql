-- A place in Bangladesh someone has been: the pins on their travel map.
--
-- Source 1 Trip: recorded when a Ghurify trip they hosted or paid for was completed (by
-- Main.SetTripsCompleted, and once for older trips by a data script).
-- Source 2 Added: they added it themselves, either a Ghurify destination or anywhere else, as a
-- named place with its own point and division.
--
-- Removing a visit archives it. Private to its owner unless they share their map, and even then
-- only visits to Ghurify destinations are shown (a self-added pin could be someone's home).
CREATE TABLE [Main].[Visit]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]         BIGINT          NOT NULL,
    [Source]         TINYINT         NOT NULL,
    [DestinationId]  BIGINT          NULL,
    [TripId]         BIGINT          NULL,
    -- A place that is not a Ghurify destination.
    [PlaceName]      NVARCHAR (120)  NULL,
    -- 1 Barishal, 2 Chattogram, 3 Dhaka, 4 Khulna, 5 Mymensingh, 6 Rajshahi, 7 Rangpur, 8 Sylhet.
    [Division]       TINYINT         NULL,
    [Location]       GEOGRAPHY       NULL,
    [VisitedOn]      DATE            NOT NULL,
    [Note]           NVARCHAR (500)  NULL,

    [Archived]       BIT             CONSTRAINT [DF_Visit_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_Visit_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_Visit_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    CONSTRAINT [PK_Visit] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Visit_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Visit_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [Main].[Destination] ([Id]),
    CONSTRAINT [FK_Visit_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [CK_Visit_Source] CHECK ([Source] BETWEEN 1 AND 2),
    CONSTRAINT [CK_Visit_Division] CHECK ([Division] IS NULL OR [Division] BETWEEN 1 AND 8),
    -- Either a Ghurify destination, or a named place with its own point and division.
    CONSTRAINT [CK_Visit_Place] CHECK ([DestinationId] IS NOT NULL
                                       OR ([PlaceName] IS NOT NULL AND [Location] IS NOT NULL AND [Division] IS NOT NULL)),
    -- A trip visit always names its trip.
    CONSTRAINT [CK_Visit_Trip] CHECK ([Source] = 2 OR [TripId] IS NOT NULL)
);
GO

-- One visit per person per trip, ever. Archived rows count too: completing a trip again, or the
-- backfill, never doubles a visit, and never brings back one its owner removed.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Visit_UserId_TripId]
    ON [Main].[Visit] ([UserId] ASC, [TripId] ASC)
    WHERE [TripId] IS NOT NULL;
GO

-- The travel map: everything one person has visited.
CREATE NONCLUSTERED INDEX [IX_Visit_UserId_VisitedOn]
    ON [Main].[Visit] ([UserId] ASC, [VisitedOn] DESC)
    INCLUDE ([Source], [DestinationId], [TripId])
    WHERE [Archived] = 0;
GO

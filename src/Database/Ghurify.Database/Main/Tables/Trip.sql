-- A trip a host offers: where, when, how many seats, and what it costs per person.
--
-- PricePerPerson must equal the sum of the trip's [Main].[TripCostItem] rows. That rule spans
-- tables, so it is enforced where trips are written (the domain and the AddTrip/SetTrip
-- procedures), not by a constraint here.
-- Temporal: every change is kept in [Main].[TripHistory], so a price or date changed after
-- people booked can always be shown to support and to the travellers affected.
CREATE TABLE [Main].[Trip]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [HostId]          BIGINT          NOT NULL,
    [DestinationId]   BIGINT          NOT NULL,
    [Title]           NVARCHAR (150)  NOT NULL,
    [Summary]         NVARCHAR (1000) NOT NULL,
    -- Calendar dates in Bangladesh. A trip runs on days, not instants, so DATE is exact.
    [StartDate]       DATE            NOT NULL,
    [EndDate]         DATE            NOT NULL,
    [MeetingPoint]    NVARCHAR (200)  NOT NULL,
    [Seats]           SMALLINT        NOT NULL,
    -- Maintained by bookings (Sprint 6). Kept on the row so search can filter by seats left
    -- without aggregating bookings for every trip.
    [SeatsTaken]      SMALLINT        CONSTRAINT [DF_Trip_SeatsTaken] DEFAULT ((0)) NOT NULL,
    [PricePerPerson]  DECIMAL (18, 2) NOT NULL,
    -- 1 Open, 2 WomenOnly, 3 Students, 4 Families.
    [GroupType]       TINYINT         CONSTRAINT [DF_Trip_GroupType] DEFAULT ((1)) NOT NULL,
    -- 1 Draft, 2 Published, 3 Full, 4 Cancelled, 5 Completed.
    [Status]          TINYINT         CONSTRAINT [DF_Trip_Status] DEFAULT ((1)) NOT NULL,
    [PublishedOn]     DATETIME2 (0)   NULL,

    [Archived]        BIT             CONSTRAINT [DF_Trip_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_Trip_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_Trip_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    [SysStartTime]    DATETIME2 (7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [SysEndTime]      DATETIME2 (7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,

    CONSTRAINT [PK_Trip] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Trip_User_HostId] FOREIGN KEY ([HostId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Trip_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [Main].[Destination] ([Id]),
    CONSTRAINT [CK_Trip_Dates] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_Trip_Seats] CHECK ([Seats] > 0 AND [SeatsTaken] >= 0 AND [SeatsTaken] <= [Seats]),
    CONSTRAINT [CK_Trip_Price] CHECK ([PricePerPerson] >= 0),
    CONSTRAINT [CK_Trip_GroupType] CHECK ([GroupType] BETWEEN 1 AND 4),
    CONSTRAINT [CK_Trip_Status] CHECK ([Status] BETWEEN 1 AND 5),
    PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Main].[TripHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- Trip search: only live trips, soonest first. Covers the filters QueryTrips applies so the
-- common search never touches the clustered index.
CREATE NONCLUSTERED INDEX [IX_Trip_Status_StartDate]
    ON [Main].[Trip] ([Status] ASC, [StartDate] ASC)
    INCLUDE ([DestinationId], [HostId], [PricePerPerson], [GroupType], [Seats], [SeatsTaken])
    WHERE [Archived] = 0;
GO

-- Upcoming trips for one destination page.
CREATE NONCLUSTERED INDEX [IX_Trip_DestinationId_StartDate]
    ON [Main].[Trip] ([DestinationId] ASC, [StartDate] ASC)
    INCLUDE ([Status], [PricePerPerson]);
GO

-- A host's own trips (GET /me/trips).
CREATE NONCLUSTERED INDEX [IX_Trip_HostId]
    ON [Main].[Trip] ([HostId] ASC)
    INCLUDE ([Status], [StartDate]);
GO

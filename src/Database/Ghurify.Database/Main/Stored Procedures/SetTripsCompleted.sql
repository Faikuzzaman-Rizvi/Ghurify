-- Marks live trips whose last day has passed as Completed, which opens reviews, and puts the trip's
-- destination on the travel map of everyone who travelled (host and paid travellers), dated by the
-- trip's first day. Set-based and idempotent: a trip already completed is not touched again, and a
-- visit is never added twice nor brought back after its owner removed it.
--
-- Returns the trips completed now, and everyone who travelled on them, for the review invitation.
CREATE PROCEDURE [Main].[SetTripsCompleted]
    @Today DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Completed TABLE ([TripId] BIGINT NOT NULL PRIMARY KEY, [HostId] BIGINT NOT NULL, [Title] NVARCHAR (150) NOT NULL);
    DECLARE @Travelled TABLE ([TripId] BIGINT NOT NULL, [UserId] BIGINT NOT NULL, [Title] NVARCHAR (150) NOT NULL,
                              PRIMARY KEY ([TripId], [UserId]));

    BEGIN TRAN;

    UPDATE [Main].[Trip]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME()
    OUTPUT inserted.[Id], inserted.[HostId], inserted.[Title] INTO @Completed ([TripId], [HostId], [Title])
    WHERE  [Status] IN (2, 3)
      AND  [EndDate] < @Today
      AND  [Archived] = 0;

    INSERT INTO @Travelled ([TripId], [UserId], [Title])
    SELECT [c].[TripId], [c].[HostId], [c].[Title]
    FROM   @Completed AS [c]
    UNION
    SELECT [c].[TripId], [b].[UserId], [c].[Title]
    FROM   @Completed      AS [c]
    JOIN   [Pay].[Booking] AS [b] ON [b].[TripId] = [c].[TripId]
    WHERE  [b].[Status] = 2;

    INSERT INTO [Main].[Visit] ([UserId], [Source], [DestinationId], [TripId], [VisitedOn])
    SELECT [p].[UserId], 1, [t].[DestinationId], [t].[Id], [t].[StartDate]
    FROM   @Travelled   AS [p]
    JOIN   [Main].[Trip] AS [t] ON [t].[Id] = [p].[TripId]
    WHERE  NOT EXISTS (SELECT 1
                       FROM   [Main].[Visit] AS [v]
                       WHERE  [v].[UserId] = [p].[UserId]
                         AND  [v].[TripId] = [p].[TripId]);

    COMMIT TRAN;

    SELECT [TripId], [UserId], [Title]
    FROM   @Travelled;
END;

-- Travel maps: puts every trip completed before the map existed on the map of everyone who
-- travelled on it (the host and paid travellers), dated by the trip's first day. From now on
-- Main.SetTripsCompleted does this as each trip completes.
--
-- Guarded, so running it again adds nothing; a visit is never added twice.

INSERT INTO [Main].[Visit] ([UserId], [Source], [DestinationId], [TripId], [VisitedOn])
SELECT [p].[UserId], 1, [t].[DestinationId], [t].[Id], [t].[StartDate]
FROM
(
    SELECT [t].[Id] AS [TripId], [t].[HostId] AS [UserId]
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Status] = 5
      AND  [t].[Archived] = 0
    UNION
    SELECT [b].[TripId], [b].[UserId]
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    WHERE  [t].[Status] = 5
      AND  [t].[Archived] = 0
      AND  [b].[Status] = 2
) AS [p]
JOIN   [Main].[Trip] AS [t] ON [t].[Id] = [p].[TripId]
WHERE  NOT EXISTS (SELECT 1
                   FROM   [Main].[Visit] AS [v]
                   WHERE  [v].[UserId] = [p].[UserId]
                     AND  [v].[TripId] = [p].[TripId]);

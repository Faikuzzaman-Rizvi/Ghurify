-- Marks live trips whose last day has passed as Completed, which opens reviews. Set-based and
-- idempotent: a trip already completed is not touched again. Returns the trips completed now, and
-- everyone who travelled on them (host and paid travellers), for the review invitation.
CREATE PROCEDURE [Main].[SetTripsCompleted]
    @Today DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Completed TABLE ([TripId] BIGINT NOT NULL PRIMARY KEY, [HostId] BIGINT NOT NULL, [Title] NVARCHAR (150) NOT NULL);

    UPDATE [Main].[Trip]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME()
    OUTPUT inserted.[Id], inserted.[HostId], inserted.[Title] INTO @Completed ([TripId], [HostId], [Title])
    WHERE  [Status] IN (2, 3)
      AND  [EndDate] < @Today
      AND  [Archived] = 0;

    SELECT [c].[TripId], [c].[HostId] AS [UserId], [c].[Title]
    FROM   @Completed AS [c]
    UNION
    SELECT [c].[TripId], [b].[UserId], [c].[Title]
    FROM   @Completed      AS [c]
    JOIN   [Pay].[Booking] AS [b] ON [b].[TripId] = [c].[TripId]
    WHERE  [b].[Status] = 2;
END;

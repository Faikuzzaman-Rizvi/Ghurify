-- Raises an SOS for someone on a trip (its host, or a traveller with a held or paid seat), and
-- returns everything the response needs in one round trip:
--   1. the SOS with who raised it, the trip, its host, and their emergency contact;
--   2. the three nearest police stations and hospitals, nearest first.
--
-- @Result 0 = raised, 1 = the person is not on this trip (nothing written, nothing returned).
CREATE PROCEDURE [Safety].[AddSosEvent]
    @UserId          BIGINT,
    @TripId          BIGINT,
    @Latitude        DECIMAL (9, 6),
    @Longitude       DECIMAL (9, 6),
    @AccuracyMeters  INT            = NULL,
    @Message         NVARCHAR (500) = NULL,
    @Id              BIGINT  OUTPUT,
    @Result          TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Id = NULL;
    SET @Result = 1;

    IF NOT EXISTS (SELECT 1
                   FROM   [Main].[Trip] AS [t]
                   WHERE  [t].[Id] = @TripId
                     AND  [t].[Archived] = 0
                     AND  ([t].[HostId] = @UserId
                           OR EXISTS (SELECT 1 FROM [Pay].[Booking] AS [b]
                                      WHERE [b].[TripId] = [t].[Id] AND [b].[UserId] = @UserId AND [b].[Status] IN (1, 2))))
    BEGIN
        RETURN;
    END;

    DECLARE @Point GEOGRAPHY = geography::Point(@Latitude, @Longitude, 4326);

    INSERT INTO [Safety].[SosEvent] ([UserId], [TripId], [Latitude], [Longitude], [AccuracyMeters], [Location], [Message], [UpdatedId])
    VALUES (@UserId, @TripId, @Latitude, @Longitude, @AccuracyMeters, @Point, @Message, @UserId);

    SET @Id = SCOPE_IDENTITY();
    SET @Result = 0;

    SELECT [s].[Id],
           [s].[UserId],
           [u].[DisplayName]           AS [UserName],
           [s].[TripId],
           [t].[Title]                 AS [TripTitle],
           [t].[HostId],
           [p].[EmergencyContactName],
           [p].[EmergencyContactPhone],
           [s].[Latitude],
           [s].[Longitude],
           [s].[Message],
           [s].[Created]
    FROM   [Safety].[SosEvent]  AS [s]
    JOIN   [Main].[User]        AS [u] ON [u].[Id] = [s].[UserId]
    JOIN   [Main].[Trip]        AS [t] ON [t].[Id] = [s].[TripId]
    LEFT JOIN [Main].[UserProfile] AS [p] ON [p].[UserId] = [s].[UserId] AND [p].[Archived] = 0
    WHERE  [s].[Id] = @Id;

    SELECT TOP (3)
           [e].[Kind],
           [e].[Name],
           [e].[NameBn],
           [e].[Phone],
           [e].[Location].[Lat]  AS [Latitude],
           [e].[Location].[Long] AS [Longitude],
           CAST([e].[Location].STDistance(@Point) AS INT) AS [DistanceMeters]
    FROM   [Safety].[EmergencyPoint] AS [e]
    WHERE  [e].[Archived] = 0
    ORDER BY [e].[Location].STDistance(@Point);
END;

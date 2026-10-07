-- A traveller's own trips: every request they made, with the trip and the booking it led to,
-- newest trip first. Filtered by @UserId: a traveller sees only their own.
CREATE PROCEDURE [Pay].[QueryMyBookings]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [r].[Id]             AS [RequestId],
             [r].[Status]         AS [RequestStatus],
             [r].[Created]        AS [RequestedOn],
             [t].[Id]             AS [TripId],
             [t].[Title],
             [d].[Slug]           AS [DestinationSlug],
             [d].[Name]           AS [DestinationName],
             [d].[NameBn]         AS [DestinationNameBn],
             [d].[Kind]           AS [DestinationKind],
             [d].[Status]         AS [DestinationStatus],
             [t].[StartDate],
             [t].[EndDate],
             [t].[Status]         AS [TripStatus],
             [h].[DisplayName]    AS [HostName],
             [b].[Id]             AS [BookingId],
             [b].[Status]         AS [BookingStatus],
             [b].[Amount],
             [b].[HoldExpiresAt]
    FROM     [Main].[JoinRequest] AS [r]
    JOIN     [Main].[Trip]        AS [t] ON [t].[Id] = [r].[TripId]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    JOIN     [Main].[User]        AS [h] ON [h].[Id] = [t].[HostId]
    LEFT JOIN [Pay].[Booking]     AS [b] ON [b].[JoinRequestId] = [r].[Id]
    WHERE    [r].[UserId] = @UserId
      AND    [r].[Archived] = 0
    ORDER BY [t].[StartDate] DESC, [r].[Id] DESC;
END;

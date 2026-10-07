-- Every trip one host runs, drafts included, newest start first. Filtered by @HostId: a host
-- sees their own trips here and nobody else's.
CREATE PROCEDURE [Main].[QueryHostTrips]
    @HostId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [t].[Id],
             [t].[Title],
             [d].[Slug]          AS [DestinationSlug],
             [d].[Name]          AS [DestinationName],
             [d].[NameBn]        AS [DestinationNameBn],
             [d].[Kind]          AS [DestinationKind],
             [d].[Status]        AS [DestinationStatus],
             [t].[StartDate],
             [t].[EndDate],
             [t].[Seats],
             [t].[SeatsTaken],
             [t].[PricePerPerson],
             [t].[GroupType],
             [t].[Status],
             (SELECT COUNT(1)
              FROM   [Main].[JoinRequest] AS [r]
              WHERE  [r].[TripId] = [t].[Id]
                AND  [r].[Status] = 1
                AND  [r].[Archived] = 0) AS [PendingRequests]
    FROM     [Main].[Trip]        AS [t]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    WHERE    [t].[HostId] = @HostId
      AND    [t].[Archived] = 0
    ORDER BY [t].[StartDate] DESC, [t].[Id] DESC;
END;

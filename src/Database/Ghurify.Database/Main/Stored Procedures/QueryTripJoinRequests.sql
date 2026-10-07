-- The host's "manage requests" page: every request for one trip, pending first, with what the host
-- needs to decide (who, their verification, their message) and the booking an approval created.
-- Filtered by @HostId: nothing comes back for a trip the caller does not host.
CREATE PROCEDURE [Main].[QueryTripJoinRequests]
    @TripId  BIGINT,
    @HostId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [r].[Id],
             [r].[UserId],
             [u].[DisplayName],
             [u].[Gender],
             (SELECT MAX([v].[Level])
              FROM   [Main].[Verification] AS [v]
              WHERE  [v].[UserId] = [u].[Id]
                AND  [v].[Status] = 2
                AND  [v].[Archived] = 0) AS [VerifiedLevel],
             [r].[Message],
             [r].[Status],
             [r].[Created],
             [b].[Id]            AS [BookingId],
             [b].[Status]        AS [BookingStatus],
             [b].[HoldExpiresAt]
    FROM     [Main].[JoinRequest] AS [r]
    JOIN     [Main].[Trip]        AS [t] ON [t].[Id] = [r].[TripId]
    JOIN     [Main].[User]        AS [u] ON [u].[Id] = [r].[UserId]
    LEFT JOIN [Pay].[Booking]     AS [b] ON [b].[JoinRequestId] = [r].[Id]
    WHERE    [r].[TripId] = @TripId
      AND    [t].[HostId] = @HostId
      AND    [r].[Archived] = 0
    ORDER BY CASE [r].[Status] WHEN 1 THEN 0 WHEN 2 THEN 1 ELSE 2 END,
             [r].[Created] ASC;
END;

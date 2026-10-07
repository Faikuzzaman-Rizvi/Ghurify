-- Releases every seat hold whose payment deadline has passed: the booking is cancelled, its join
-- request marked expired, and the seat handed back to the trip (a Full trip opens again).
--
-- Set-based and idempotent: only bookings still Held are touched, so running it twice, or two
-- workers running it at once, releases each seat exactly once. Returns the released bookings so
-- the caller can tell the traveller and the host.
CREATE PROCEDURE [Pay].[SetBookingHoldsExpired]
    @Now DATETIME2 (0)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Expired TABLE
    (
        [BookingId]      BIGINT NOT NULL PRIMARY KEY,
        [TripId]         BIGINT NOT NULL,
        [UserId]         BIGINT NOT NULL,
        [JoinRequestId]  BIGINT NOT NULL
    );

    BEGIN TRAN;

    UPDATE [Pay].[Booking]
    SET    [Status]      = 3,
           [CancelledOn] = @Now,
           [UpdatedOn]   = SYSUTCDATETIME()
    OUTPUT inserted.[Id], inserted.[TripId], inserted.[UserId], inserted.[JoinRequestId]
    INTO   @Expired ([BookingId], [TripId], [UserId], [JoinRequestId])
    WHERE  [Status] = 1
      AND  [HoldExpiresAt] <= @Now
      AND  [Archived] = 0;

    UPDATE [t]
    SET    [SeatsTaken] = CASE WHEN [t].[SeatsTaken] >= [x].[Released]
                               THEN [t].[SeatsTaken] - [x].[Released] ELSE 0 END,
           [Status]     = CASE WHEN [t].[Status] = 3 THEN 2 ELSE [t].[Status] END,
           [UpdatedOn]  = SYSUTCDATETIME()
    FROM   [Main].[Trip] AS [t]
    JOIN   (SELECT [TripId], COUNT(1) AS [Released] FROM @Expired GROUP BY [TripId]) AS [x]
           ON [x].[TripId] = [t].[Id];

    -- Any payment attempt still in flight for a released seat is over too. If its money arrives
    -- after all, the payment is settled as paid-late (re-seated if possible, otherwise refunded).
    UPDATE [p]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME()
    FROM   [Pay].[Payment] AS [p]
    JOIN   @Expired        AS [e] ON [e].[BookingId] = [p].[BookingId]
    WHERE  [p].[Status] IN (1, 2);

    UPDATE [r]
    SET    [Status]    = 4,
           [UpdatedOn] = SYSUTCDATETIME()
    FROM   [Main].[JoinRequest] AS [r]
    JOIN   @Expired             AS [e] ON [e].[JoinRequestId] = [r].[Id]
    WHERE  [r].[Status] = 2;

    COMMIT TRAN;

    SELECT [e].[BookingId],
           [e].[TripId],
           [e].[UserId],
           [t].[HostId],
           [t].[Title] AS [TripTitle]
    FROM   @Expired     AS [e]
    JOIN   [Main].[Trip] AS [t] ON [t].[Id] = [e].[TripId];
END;

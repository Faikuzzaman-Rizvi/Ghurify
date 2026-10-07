-- What the refund rules need to price a traveller cancelling their own booking: the trip price paid,
-- the service fee paid, the start date and where things stand. Filtered by @UserId.
CREATE PROCEDURE [Pay].[GetBookingCancellation]
    @BookingId  BIGINT,
    @UserId     BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [b].[Id]          AS [BookingId],
           [b].[TripId],
           [t].[Title]       AS [TripTitle],
           [t].[StartDate],
           [t].[Status]      AS [TripStatus],
           [b].[Status],
           [b].[Amount],
           ISNULL([pay].[Fee], 0) AS [Fee]
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    OUTER APPLY (SELECT TOP (1) [p].[Fee]
                 FROM   [Pay].[Payment] AS [p]
                 WHERE  [p].[BookingId] = [b].[Id]
                   AND  [p].[Status] = 3
                   AND  [p].[FailureReason] IS NULL
                 ORDER BY [p].[Id] DESC) AS [pay]
    WHERE  [b].[Id] = @BookingId
      AND  [b].[UserId] = @UserId
      AND  [b].[Archived] = 0;
END;

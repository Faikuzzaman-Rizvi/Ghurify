-- What the checkout page and the payment result pages show for a traveller's own booking: the
-- trip, the amount, the hold deadline, where the booking stands and its latest payment attempt.
-- Filtered by @UserId: a traveller can only see their own.
CREATE PROCEDURE [Pay].[GetBookingCheckout]
    @BookingId  BIGINT,
    @UserId     BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [b].[Id]             AS [BookingId],
           [b].[TripId],
           [t].[Title]          AS [TripTitle],
           [t].[StartDate],
           [t].[EndDate],
           [h].[DisplayName]    AS [HostName],
           [b].[Amount],
           [b].[Status],
           [b].[HoldExpiresAt],
           [p].[Status]         AS [LatestPaymentStatus],
           [p].[FailureReason]  AS [LatestPaymentFailure]
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    JOIN   [Main].[User]   AS [h] ON [h].[Id] = [t].[HostId]
    OUTER APPLY (SELECT TOP (1) [Status], [FailureReason]
                 FROM   [Pay].[Payment]
                 WHERE  [BookingId] = [b].[Id]
                 ORDER BY [Id] DESC) AS [p]
    WHERE  [b].[Id] = @BookingId
      AND  [b].[UserId] = @UserId
      AND  [b].[Archived] = 0;
END;

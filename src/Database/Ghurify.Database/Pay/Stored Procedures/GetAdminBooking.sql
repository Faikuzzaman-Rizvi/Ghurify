-- One booking with its money, for the admin desk: found by booking id, or by a payment's
-- transaction reference (what a traveller quotes when they write in).
--   1. the booking, trip and traveller;
--   2. its payment attempts;
--   3. its refunds;
--   4. the escrow ledger totals (held, released to the host, refunded, and what is left).
-- Nothing is returned when there is no such booking.
CREATE PROCEDURE [Pay].[GetAdminBooking]
    @BookingId  BIGINT       = NULL,
    @Reference  VARCHAR (64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id BIGINT = @BookingId;
    IF @Id IS NULL AND @Reference IS NOT NULL
    BEGIN
        SELECT TOP (1) @Id = [BookingId]
        FROM   [Pay].[Payment]
        WHERE  [TransactionRef] = @Reference OR [ProviderTxnId] = @Reference;
    END;

    IF NOT EXISTS (SELECT 1 FROM [Pay].[Booking] WHERE [Id] = @Id AND [Archived] = 0)
    BEGIN
        RETURN;
    END;

    SELECT [b].[Id],
           [b].[TripId],
           [t].[Title]       AS [TripTitle],
           [t].[StartDate],
           [t].[HostId],
           [h].[DisplayName] AS [HostName],
           [b].[UserId],
           [u].[DisplayName] AS [TravellerName],
           [b].[Status],
           [b].[Amount],
           [b].[Created],
           [b].[ConfirmedOn],
           [b].[CancelledOn]
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    JOIN   [Main].[User]   AS [h] ON [h].[Id] = [t].[HostId]
    JOIN   [Main].[User]   AS [u] ON [u].[Id] = [b].[UserId]
    WHERE  [b].[Id] = @Id;

    SELECT   [Id], [Provider], [TransactionRef], [Status], [Amount], [Fee], [Total], [PaidAmount], [FailureReason], [Created], [CompletedOn]
    FROM     [Pay].[Payment]
    WHERE    [BookingId] = @Id
    ORDER BY [Id] DESC;

    SELECT   [Id], [Amount], [Reason], [Status], [Reference], [FailureReason], [Attempts], [Created], [CompletedOn]
    FROM     [Pay].[Refund]
    WHERE    [BookingId] = @Id
    ORDER BY [Id] DESC;

    SELECT ISNULL(SUM(CASE WHEN [EntryType] = 1 THEN [Amount] END), 0) AS [Held],
           ISNULL(SUM(CASE WHEN [EntryType] = 2 THEN [Amount] END), 0) AS [Released],
           ISNULL(SUM(CASE WHEN [EntryType] = 3 THEN [Amount] END), 0) AS [Refunded]
    FROM   [Pay].[EscrowLedger]
    WHERE  [BookingId] = @Id;
END;

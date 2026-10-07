-- Creates several refunds in one transaction (a host cancelling a trip, a destination closing): for
-- each booking, a refund row and its ledger Refund entry, each capped at what escrow still holds for
-- that booking (the rest recorded as Shortfall). Set-based; the bookings are locked first so a
-- payout running at the same moment cannot spend the same balance.
--
-- Idempotent on Reference: a booking whose refund reference already exists is skipped and its
-- existing refund returned. Bookings with nothing left in escrow, or no succeeded payment, get no
-- refund and come back with RefundId NULL.
CREATE PROCEDURE [Pay].[AddRefunds]
    @Requests  [Pay].[RefundRequestList] READONLY,
    @ActorId   BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    -- Lock the bookings: the mutex for each one's ledger.
    DECLARE @Locked TABLE ([Id] BIGINT PRIMARY KEY);
    INSERT INTO @Locked ([Id])
    SELECT [b].[Id]
    FROM   [Pay].[Booking] AS [b] WITH (UPDLOCK, HOLDLOCK)
    JOIN   @Requests       AS [q] ON [q].[BookingId] = [b].[Id];

    DECLARE @Plan TABLE
    (
        [BookingId]  BIGINT          NOT NULL PRIMARY KEY,
        [PaymentId]  BIGINT          NULL,
        [Requested]  DECIMAL (18, 2) NOT NULL,
        [Amount]     DECIMAL (18, 2) NOT NULL,
        [Reason]     TINYINT         NOT NULL,
        [Reference]  VARCHAR (100)   NOT NULL
    );

    INSERT INTO @Plan ([BookingId], [PaymentId], [Requested], [Amount], [Reason], [Reference])
    SELECT [q].[BookingId],
           [pay].[Id],
           [q].[Amount],
           CASE WHEN [q].[Amount] > ISNULL([bal].[Balance], 0) THEN ISNULL([bal].[Balance], 0) ELSE [q].[Amount] END,
           [q].[Reason],
           [q].[Reference]
    FROM   @Requests AS [q]
    OUTER APPLY (SELECT SUM(CASE WHEN [l].[EntryType] = 1 THEN [l].[Amount] ELSE -[l].[Amount] END) AS [Balance]
                 FROM   [Pay].[EscrowLedger] AS [l]
                 WHERE  [l].[BookingId] = [q].[BookingId]) AS [bal]
    OUTER APPLY (SELECT TOP (1) [p].[Id]
                 FROM   [Pay].[Payment] AS [p]
                 WHERE  [p].[BookingId] = [q].[BookingId]
                   AND  [p].[Status] = 3
                 ORDER BY [p].[Id] DESC) AS [pay]
    WHERE  NOT EXISTS (SELECT 1 FROM [Pay].[Refund] AS [r] WHERE [r].[Reference] = [q].[Reference]);

    DECLARE @Created TABLE ([RefundId] BIGINT NOT NULL, [BookingId] BIGINT NOT NULL, [PaymentId] BIGINT NOT NULL, [Amount] DECIMAL (18, 2) NOT NULL);

    INSERT INTO [Pay].[Refund] ([BookingId], [PaymentId], [Amount], [Shortfall], [Reason], [Reference], [UpdatedId])
    OUTPUT inserted.[Id], inserted.[BookingId], inserted.[PaymentId], inserted.[Amount]
    INTO   @Created ([RefundId], [BookingId], [PaymentId], [Amount])
    SELECT [BookingId], [PaymentId], [Amount], [Requested] - [Amount], [Reason], [Reference], @ActorId
    FROM   @Plan
    WHERE  [Amount] > 0
      AND  [PaymentId] IS NOT NULL;

    INSERT INTO [Pay].[EscrowLedger] ([BookingId], [PaymentId], [EntryType], [Amount], [Counterparty], [Reference], [UpdatedId])
    SELECT [BookingId], [PaymentId], 3, [Amount], 1, CONCAT('refund:', [RefundId]), @ActorId
    FROM   @Created;

    COMMIT TRAN;

    -- Every requested booking: its refund (new or existing), or none.
    SELECT [q].[BookingId],
           [r].[Id]        AS [RefundId],
           ISNULL([r].[Amount], 0)            AS [Amount],
           ISNULL([r].[Shortfall], [q].[Amount]) AS [Shortfall]
    FROM   @Requests AS [q]
    LEFT JOIN [Pay].[Refund] AS [r] ON [r].[Reference] = [q].[Reference];
END;

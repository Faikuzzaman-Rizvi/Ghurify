-- Releases every payout that has fallen due, for all trips at once, in one transaction.
--
-- Stage 1 (before departure): for live trips starting within @FirstStageDaysBefore days, a share
--   (@FirstStagePercent of the trip price) of every paid seat goes to the host.
-- Stage 2 (after the trip starts): for trips that have started, everything still in escrow goes
--   out: the service fee to the platform, the rest to the host.
--
-- Each stage of each trip is released once: the Payout row is unique on (TripId, Stage) and every
-- ledger Release entry has a unique reference, so running this twice releases nothing twice.
-- The bookings involved are locked, so a refund at the same moment cannot spend the same balance,
-- and no release ever exceeds a booking's balance: the ledger never goes negative.
--
-- Returns the payouts created by this run, for notifying hosts.
CREATE PROCEDURE [Pay].[SetDuePayouts]
    @Today                 DATE,
    @FirstStageDaysBefore  INT,
    @FirstStagePercent     DECIMAL (5, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Due TABLE ([TripId] BIGINT NOT NULL, [HostId] BIGINT NOT NULL, [Stage] TINYINT NOT NULL,
                        PRIMARY KEY ([TripId], [Stage]));

    BEGIN TRAN;

    INSERT INTO @Due ([TripId], [HostId], [Stage])
    SELECT [t].[Id], [t].[HostId], 1
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Archived] = 0
      AND  [t].[Status] IN (2, 3)
      AND  [t].[StartDate] <= DATEADD(DAY, @FirstStageDaysBefore, @Today)
      AND  [t].[StartDate] > @Today
      AND  NOT EXISTS (SELECT 1 FROM [Pay].[Payout] AS [p] WHERE [p].[TripId] = [t].[Id] AND [p].[Stage] = 1)
    UNION ALL
    SELECT [t].[Id], [t].[HostId], 2
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Archived] = 0
      AND  [t].[Status] IN (2, 3, 5)
      AND  [t].[StartDate] <= @Today
      AND  NOT EXISTS (SELECT 1 FROM [Pay].[Payout] AS [p] WHERE [p].[TripId] = [t].[Id] AND [p].[Stage] = 2);

    -- Lock every booking on those trips: the ledger mutex, as in AddRefund.
    DECLARE @Bookings TABLE ([BookingId] BIGINT NOT NULL PRIMARY KEY, [TripId] BIGINT NOT NULL,
                             [Status] TINYINT NOT NULL, [Amount] DECIMAL (18, 2) NOT NULL);

    INSERT INTO @Bookings ([BookingId], [TripId], [Status], [Amount])
    SELECT [b].[Id], [b].[TripId], [b].[Status], [b].[Amount]
    FROM   [Pay].[Booking] AS [b] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [b].[TripId] IN (SELECT [TripId] FROM @Due)
      AND  [b].[Archived] = 0;

    -- Each booking's balance and the fee its traveller paid.
    DECLARE @Balances TABLE ([BookingId] BIGINT NOT NULL PRIMARY KEY, [TripId] BIGINT NOT NULL, [Status] TINYINT NOT NULL,
                             [Amount] DECIMAL (18, 2) NOT NULL, [Balance] DECIMAL (18, 2) NOT NULL, [Fee] DECIMAL (18, 2) NOT NULL,
                             [PaymentId] BIGINT NULL);

    INSERT INTO @Balances ([BookingId], [TripId], [Status], [Amount], [Balance], [Fee], [PaymentId])
    SELECT [b].[BookingId], [b].[TripId], [b].[Status], [b].[Amount],
           ISNULL((SELECT SUM(CASE WHEN [l].[EntryType] = 1 THEN [l].[Amount] ELSE -[l].[Amount] END)
                   FROM   [Pay].[EscrowLedger] AS [l]
                   WHERE  [l].[BookingId] = [b].[BookingId]), 0),
           ISNULL([pay].[Fee], 0),
           [pay].[Id]
    FROM   @Bookings AS [b]
    OUTER APPLY (SELECT TOP (1) [p].[Id], [p].[Fee]
                 FROM   [Pay].[Payment] AS [p]
                 WHERE  [p].[BookingId] = [b].[BookingId]
                   AND  [p].[Status] = 3
                   AND  [p].[FailureReason] IS NULL
                 ORDER BY [p].[Id] DESC) AS [pay];

    -- What each booking releases: (BookingId, Stage, Counterparty 2 host / 3 platform, Amount).
    DECLARE @Releases TABLE ([BookingId] BIGINT NOT NULL, [TripId] BIGINT NOT NULL, [PaymentId] BIGINT NULL,
                             [Stage] TINYINT NOT NULL, [Counterparty] TINYINT NOT NULL, [Amount] DECIMAL (18, 2) NOT NULL);

    -- Stage 1: a share of each paid seat, never more than its balance.
    INSERT INTO @Releases ([BookingId], [TripId], [PaymentId], [Stage], [Counterparty], [Amount])
    SELECT [x].[BookingId], [x].[TripId], [x].[PaymentId], 1, 2,
           CASE WHEN ROUND([x].[Amount] * @FirstStagePercent / 100.0, 2) > [x].[Balance]
                THEN [x].[Balance] ELSE ROUND([x].[Amount] * @FirstStagePercent / 100.0, 2) END
    FROM   @Balances AS [x]
    JOIN   @Due      AS [d] ON [d].[TripId] = [x].[TripId] AND [d].[Stage] = 1
    WHERE  [x].[Status] = 2
      AND  [x].[Balance] > 0;

    -- Stage 2: the fee to the platform, then everything else to the host, from what is left after
    -- any stage-1 release in this same run.
    WITH [Left] AS
    (
        SELECT [x].[BookingId], [x].[TripId], [x].[PaymentId], [x].[Fee],
               [x].[Balance] - ISNULL((SELECT SUM([r].[Amount]) FROM @Releases AS [r] WHERE [r].[BookingId] = [x].[BookingId]), 0) AS [Remaining]
        FROM   @Balances AS [x]
        JOIN   @Due      AS [d] ON [d].[TripId] = [x].[TripId] AND [d].[Stage] = 2
    )
    INSERT INTO @Releases ([BookingId], [TripId], [PaymentId], [Stage], [Counterparty], [Amount])
    SELECT [BookingId], [TripId], [PaymentId], 2, 3,
           CASE WHEN [Fee] > [Remaining] THEN [Remaining] ELSE [Fee] END
    FROM   [Left]
    WHERE  [Remaining] > 0 AND [Fee] > 0
    UNION ALL
    SELECT [BookingId], [TripId], [PaymentId], 2, 2,
           [Remaining] - CASE WHEN [Fee] > [Remaining] THEN [Remaining] ELSE [Fee] END
    FROM   [Left]
    WHERE  [Remaining] - CASE WHEN [Fee] > [Remaining] THEN [Remaining] ELSE [Fee] END > 0;

    -- One payout row per due trip and stage, even when nothing was owed, so it is never re-run.
    DECLARE @Created TABLE ([PayoutId] BIGINT NOT NULL, [TripId] BIGINT NOT NULL, [HostId] BIGINT NOT NULL,
                            [Stage] TINYINT NOT NULL, [Amount] DECIMAL (18, 2) NOT NULL);

    INSERT INTO [Pay].[Payout] ([TripId], [HostId], [Stage], [Amount], [PlatformAmount])
    OUTPUT inserted.[Id], inserted.[TripId], inserted.[HostId], inserted.[Stage], inserted.[Amount]
    INTO   @Created ([PayoutId], [TripId], [HostId], [Stage], [Amount])
    SELECT [d].[TripId], [d].[HostId], [d].[Stage],
           ISNULL((SELECT SUM([r].[Amount]) FROM @Releases AS [r]
                   WHERE [r].[TripId] = [d].[TripId] AND [r].[Stage] = [d].[Stage] AND [r].[Counterparty] = 2), 0),
           ISNULL((SELECT SUM([r].[Amount]) FROM @Releases AS [r]
                   WHERE [r].[TripId] = [d].[TripId] AND [r].[Stage] = [d].[Stage] AND [r].[Counterparty] = 3), 0)
    FROM   @Due AS [d];

    INSERT INTO [Pay].[EscrowLedger] ([BookingId], [PaymentId], [EntryType], [Amount], [Counterparty], [Reference])
    SELECT [r].[BookingId], [r].[PaymentId], 2, [r].[Amount], [r].[Counterparty],
           CONCAT('release:', CASE WHEN [r].[Counterparty] = 2 THEN 'host' ELSE 'platform' END, ':', [r].[Stage], ':', [r].[BookingId])
    FROM   @Releases AS [r]
    WHERE  [r].[Amount] > 0
      AND  NOT EXISTS (SELECT 1 FROM [Pay].[EscrowLedger] AS [l]
                       WHERE [l].[Reference] = CONCAT('release:', CASE WHEN [r].[Counterparty] = 2 THEN 'host' ELSE 'platform' END,
                                                      ':', [r].[Stage], ':', [r].[BookingId]));

    COMMIT TRAN;

    SELECT [c].[PayoutId], [c].[TripId], [c].[HostId], [c].[Stage], [c].[Amount], [t].[Title] AS [TripTitle]
    FROM   @Created     AS [c]
    JOIN   [Main].[Trip] AS [t] ON [t].[Id] = [c].[TripId];
END;

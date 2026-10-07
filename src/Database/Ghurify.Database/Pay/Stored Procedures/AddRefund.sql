-- Takes money out of escrow for a traveller, as one transaction: the refund row and its ledger
-- Refund entry. The gateway call happens afterwards; a failed call leaves the refund Pending/Failed
-- for the retry job, and escrow already reflects that the money is owed back.
--
-- The booking row is locked as the mutex for its ledger, so a refund and a payout cannot both see
-- the same balance. The refund is capped at what escrow still holds; anything above that is
-- recorded as Shortfall for the admin desk. The ledger can never go negative.
-- Idempotent on @Reference: asking for the same refund again returns the first one.
--
-- @Result 0 = created, 1 = already existed (returned as is), 2 = nothing left in escrow to refund,
-- 3 = no succeeded payment to refund against.
CREATE PROCEDURE [Pay].[AddRefund]
    @BookingId        BIGINT,
    @RequestedAmount  DECIMAL (18, 2),
    @Reason           TINYINT,
    @Reference        VARCHAR (100),
    @ActorId          BIGINT = NULL,
    @PaymentId        BIGINT = NULL,
    @RefundId         BIGINT          OUTPUT,
    @Amount           DECIMAL (18, 2) OUTPUT,
    @Shortfall        DECIMAL (18, 2) OUTPUT,
    @Result           TINYINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @RefundId = NULL;
    SET @Result = 2;

    BEGIN TRAN;

    -- The mutex for this booking's ledger.
    DECLARE @Locked BIGINT;
    SELECT @Locked = [Id] FROM [Pay].[Booking] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @BookingId;

    SELECT @RefundId  = [Id],
           @Amount    = [Amount],
           @Shortfall = [Shortfall]
    FROM   [Pay].[Refund]
    WHERE  [Reference] = @Reference;

    IF @RefundId IS NOT NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    -- Refund against the given payment, or the booking's most recent succeeded one.
    IF @PaymentId IS NULL
    BEGIN
        SELECT TOP (1) @PaymentId = [Id]
        FROM   [Pay].[Payment]
        WHERE  [BookingId] = @BookingId
          AND  [Status] = 3
        ORDER BY [Id] DESC;
    END;

    IF @PaymentId IS NULL
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @Balance DECIMAL (18, 2);

    SELECT @Balance = ISNULL(SUM(CASE WHEN [EntryType] = 1 THEN [Amount] ELSE -[Amount] END), 0)
    FROM   [Pay].[EscrowLedger]
    WHERE  [BookingId] = @BookingId;

    IF @Balance <= 0
    BEGIN
        SET @Amount = 0;
        SET @Shortfall = @RequestedAmount;
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    SET @Amount = CASE WHEN @RequestedAmount > @Balance THEN @Balance ELSE @RequestedAmount END;
    SET @Shortfall = @RequestedAmount - @Amount;

    INSERT INTO [Pay].[Refund] ([BookingId], [PaymentId], [Amount], [Shortfall], [Reason], [Reference], [UpdatedId])
    VALUES (@BookingId, @PaymentId, @Amount, @Shortfall, @Reason, @Reference, @ActorId);

    SET @RefundId = SCOPE_IDENTITY();

    INSERT INTO [Pay].[EscrowLedger] ([BookingId], [PaymentId], [EntryType], [Amount], [Counterparty], [Reference], [UpdatedId])
    VALUES (@BookingId, @PaymentId, 3, @Amount, 1, CONCAT('refund:', @RefundId), @ActorId);

    SET @Result = 0;

    COMMIT TRAN;
END;

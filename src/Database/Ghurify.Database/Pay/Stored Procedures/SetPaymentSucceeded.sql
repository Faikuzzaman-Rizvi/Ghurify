-- Settles a payment the gateway has confirmed server-to-server, as one transaction: the payment is
-- marked succeeded, the money enters escrow (a ledger Hold), and the booking is confirmed.
--
-- Safe to call any number of times for the same payment (duplicate webhooks, the browser return
-- and the IPN both arriving): the second call finds it succeeded and changes nothing.
--
-- @Result
--   0 = confirmed: payment succeeded, ledger Hold written, booking Confirmed.
--   1 = no payment with this transaction reference.
--   2 = already settled as succeeded; nothing changed.
--   3 = the amount or currency paid is not what was asked: the money is held (ledger Hold of
--       what was actually paid) but the booking is NOT confirmed; the caller refunds it.
--   4 = paid, but the booking can no longer be confirmed (its seat was released and the trip is
--       now full, or another payment already confirmed it): the money is held and the caller
--       refunds it.
--   5 = this gateway transaction id was already used for a different payment; nothing changed.
--
-- @MethodType ... @RiskFlagged are what the gateway's validation reported about how the traveller
-- paid. They are recorded with the payment and never change the outcome.
CREATE PROCEDURE [Pay].[SetPaymentSucceeded]
    @TransactionRef  VARCHAR (40),
    @ProviderTxnId   VARCHAR (100),
    @PaidAmount      DECIMAL (18, 2),
    @Currency        CHAR (3),
    @MethodType      TINYINT         = NULL,
    @MethodName      NVARCHAR (60)   = NULL,
    @AccountLast4    VARCHAR (4)     = NULL,
    @Issuer          NVARCHAR (100)  = NULL,
    @ValidationId    VARCHAR (100)   = NULL,
    @GatewayPaidOn   DATETIME2 (0)   = NULL,
    @StoreAmount     DECIMAL (18, 2) = NULL,
    @RiskFlagged     BIT             = NULL,
    @PaymentId       BIGINT          OUTPUT,
    @BookingId       BIGINT          OUTPUT,
    @UserId          BIGINT          OUTPUT,
    @HostId          BIGINT          OUTPUT,
    @TripId          BIGINT          OUTPUT,
    @Result          TINYINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @PaymentId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @Total DECIMAL (18, 2), @Provider VARCHAR (20);

    SELECT @PaymentId = [Id],
           @BookingId = [BookingId],
           @UserId    = [UserId],
           @Status    = [Status],
           @Total     = [Total],
           @Provider  = [Provider]
    FROM   [Pay].[Payment] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [TransactionRef] = @TransactionRef;

    IF @PaymentId IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    SELECT @TripId = [b].[TripId],
           @HostId = [t].[HostId]
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    WHERE  [b].[Id] = @BookingId;

    IF @Status = 3
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF EXISTS (SELECT 1
               FROM   [Pay].[Payment]
               WHERE  [Provider] = @Provider
                 AND  [ProviderTxnId] = @ProviderTxnId
                 AND  [Id] <> @PaymentId)
    BEGIN
        SET @Result = 5;
        COMMIT TRAN;
        RETURN;
    END;

    -- The money has arrived whatever happens next: record it and hold it in escrow.
    UPDATE [Pay].[Payment]
    SET    [Status]        = 3,
           [ProviderTxnId] = @ProviderTxnId,
           [PaidAmount]    = @PaidAmount,
           [CompletedOn]   = SYSUTCDATETIME(),
           [FailureReason] = CASE WHEN @PaidAmount <> @Total OR @Currency <> [Currency]
                                  THEN N'amount_mismatch' ELSE NULL END,
           [MethodType]    = @MethodType,
           [MethodName]    = @MethodName,
           [AccountLast4]  = @AccountLast4,
           [Issuer]        = @Issuer,
           [ValidationId]  = @ValidationId,
           [GatewayPaidOn] = @GatewayPaidOn,
           [StoreAmount]   = @StoreAmount,
           [RiskFlagged]   = @RiskFlagged,
           [UpdatedOn]     = SYSUTCDATETIME()
    WHERE  [Id] = @PaymentId;

    IF @PaidAmount > 0
    BEGIN
        INSERT INTO [Pay].[EscrowLedger] ([BookingId], [PaymentId], [EntryType], [Amount], [Counterparty], [Reference])
        VALUES (@BookingId, @PaymentId, 1, @PaidAmount, 1, CONCAT('hold:payment:', @PaymentId));
    END;

    IF @PaidAmount <> @Total OR @Currency <> 'BDT'
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @BookingStatus TINYINT, @RequestId BIGINT, @RequestStatus TINYINT;

    SELECT @BookingStatus = [b].[Status],
           @RequestId     = [b].[JoinRequestId],
           @RequestStatus = [r].[Status]
    FROM   [Pay].[Booking]      AS [b] WITH (UPDLOCK, HOLDLOCK)
    JOIN   [Main].[JoinRequest] AS [r] ON [r].[Id] = [b].[JoinRequestId]
    WHERE  [b].[Id] = @BookingId;

    IF @BookingStatus = 1
    BEGIN
        UPDATE [Pay].[Booking]
        SET    [Status]      = 2,
               [ConfirmedOn] = SYSUTCDATETIME(),
               [UpdatedOn]   = SYSUTCDATETIME(),
               [UpdatedId]   = @UserId
        WHERE  [Id] = @BookingId;

        SET @Result = 0;
        COMMIT TRAN;
        RETURN;
    END;

    -- Paid after the hold ran out. If the seat is still free, take it again rather than refund:
    -- the traveller did pay, just late.
    IF @BookingStatus = 3 AND @RequestStatus = 4
    BEGIN
        UPDATE [Main].[Trip]
        SET    [SeatsTaken] = [SeatsTaken] + 1,
               [Status]     = CASE WHEN [SeatsTaken] + 1 >= [Seats] THEN 3 ELSE [Status] END,
               [UpdatedOn]  = SYSUTCDATETIME()
        WHERE  [Id] = @TripId
          AND  [Status] = 2
          AND  [SeatsTaken] < [Seats];

        IF @@ROWCOUNT = 1
        BEGIN
            UPDATE [Pay].[Booking]
            SET    [Status]      = 2,
                   [ConfirmedOn] = SYSUTCDATETIME(),
                   [CancelledOn] = NULL,
                   [UpdatedOn]   = SYSUTCDATETIME(),
                   [UpdatedId]   = @UserId
            WHERE  [Id] = @BookingId;

            UPDATE [Main].[JoinRequest]
            SET    [Status]    = 2,
                   [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [Id] = @RequestId;

            SET @Result = 0;
            COMMIT TRAN;
            RETURN;
        END;
    END;

    -- Already confirmed by another payment, withdrawn, or no seat left: hold, then refund.
    SET @Result = 4;

    COMMIT TRAN;
END;

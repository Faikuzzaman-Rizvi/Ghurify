-- Starts a payment attempt for a traveller's own held booking, or replays an earlier attempt that
-- used the same idempotency key. One transaction:
--   - the key is looked up first: the same key for the same booking returns the existing attempt
--     (a retried request charges nothing new); the same key on another booking, or another user's
--     key, is refused without revealing anything about it;
--   - the booking must be the caller's, Held, and still inside its payment window;
--   - the amount is the booking's, the fee is computed here, and Total = Amount + Fee.
--
-- @Result 0 = created, 1 = replay of an existing attempt, 2 = booking not found (or not theirs),
-- 3 = already paid, 4 = not payable (released, or the hold has expired), 5 = key already used.
CREATE PROCEDURE [Pay].[AddPayment]
    @BookingId       BIGINT,
    @UserId          BIGINT,
    @IdempotencyKey  VARCHAR (64),
    @Provider        VARCHAR (20),
    @TransactionRef  VARCHAR (40),
    @FeePercent      DECIMAL (5, 2),
    @Now             DATETIME2 (0),
    @PaymentId       BIGINT          OUTPUT,
    @Amount          DECIMAL (18, 2) OUTPUT,
    @Fee             DECIMAL (18, 2) OUTPUT,
    @Total           DECIMAL (18, 2) OUTPUT,
    @Status          TINYINT         OUTPUT,
    @RedirectUrl     NVARCHAR (500)  OUTPUT,
    @OutTransactionRef VARCHAR (40)  OUTPUT,
    @Result          TINYINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @PaymentId = NULL;
    SET @Result = 2;

    BEGIN TRAN;

    DECLARE @ExistingBooking BIGINT, @ExistingUser BIGINT;

    SELECT @PaymentId         = [Id],
           @ExistingBooking   = [BookingId],
           @ExistingUser      = [UserId],
           @Amount            = [Amount],
           @Fee               = [Fee],
           @Total             = [Total],
           @Status            = [Status],
           @RedirectUrl       = [RedirectUrl],
           @OutTransactionRef = [TransactionRef]
    FROM   [Pay].[Payment] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [IdempotencyKey] = @IdempotencyKey;

    IF @PaymentId IS NOT NULL
    BEGIN
        IF @ExistingUser <> @UserId OR @ExistingBooking <> @BookingId
        BEGIN
            SET @PaymentId = NULL;
            SET @Amount = NULL;
            SET @Fee = NULL;
            SET @Total = NULL;
            SET @Status = NULL;
            SET @RedirectUrl = NULL;
            SET @OutTransactionRef = NULL;
            SET @Result = 5;
        END
        ELSE
        BEGIN
            SET @Result = 1;
        END;

        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @BookingStatus TINYINT, @HoldExpiresAt DATETIME2 (0);

    SELECT @BookingStatus = [Status],
           @HoldExpiresAt = [HoldExpiresAt],
           @Amount        = [Amount]
    FROM   [Pay].[Booking] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Id] = @BookingId
      AND  [UserId] = @UserId
      AND  [Archived] = 0;

    IF @BookingStatus IS NULL
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @BookingStatus = 2
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF @BookingStatus <> 1 OR @HoldExpiresAt <= @Now
    BEGIN
        SET @Result = 4;
        COMMIT TRAN;
        RETURN;
    END;

    SET @Fee = ROUND(@Amount * @FeePercent / 100.0, 2);
    SET @Total = @Amount + @Fee;
    SET @Status = 1;
    SET @OutTransactionRef = @TransactionRef;

    INSERT INTO [Pay].[Payment]
           ([BookingId], [UserId], [Provider], [IdempotencyKey], [TransactionRef], [Amount], [Fee], [Total], [Status], [UpdatedId])
    VALUES (@BookingId, @UserId, @Provider, @IdempotencyKey, @TransactionRef, @Amount, @Fee, @Total, 1, @UserId);

    SET @PaymentId = SCOPE_IDENTITY();
    SET @Result = 0;

    COMMIT TRAN;
END;

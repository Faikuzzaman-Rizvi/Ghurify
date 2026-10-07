-- Marks an attempt failed: the gateway refused it, the traveller cancelled, or the gateway could
-- not be reached. Only an attempt still in flight (Created or Pending) changes, so a failure
-- report arriving after a success can never undo it.
--
-- Found by @PaymentId or by @TransactionRef (from a gateway callback).
-- @Result 0 = marked failed, 1 = not found, 2 = already settled (nothing changed).
CREATE PROCEDURE [Pay].[SetPaymentFailed]
    @PaymentId       BIGINT        = NULL,
    @TransactionRef  VARCHAR (40)  = NULL,
    @Reason          NVARCHAR (300),
    @Result          TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [Pay].[Payment]
    SET    [Status]        = 4,
           [FailureReason] = @Reason,
           [CompletedOn]   = SYSUTCDATETIME(),
           [UpdatedOn]     = SYSUTCDATETIME()
    WHERE  ((@PaymentId IS NOT NULL AND [Id] = @PaymentId)
            OR (@PaymentId IS NULL AND [TransactionRef] = @TransactionRef))
      AND  [Status] IN (1, 2);

    IF @@ROWCOUNT > 0
    BEGIN
        SET @Result = 0;
        RETURN;
    END;

    SET @Result = CASE WHEN EXISTS (SELECT 1
                                    FROM   [Pay].[Payment]
                                    WHERE  (@PaymentId IS NOT NULL AND [Id] = @PaymentId)
                                       OR  (@PaymentId IS NULL AND [TransactionRef] = @TransactionRef))
                       THEN 2 ELSE 1 END;
END;

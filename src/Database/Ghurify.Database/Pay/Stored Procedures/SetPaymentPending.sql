-- Records that the gateway accepted the attempt and where the traveller was sent to pay.
-- Only a Created attempt moves to Pending, so a late answer cannot undo a success or failure.
CREATE PROCEDURE [Pay].[SetPaymentPending]
    @PaymentId          BIGINT,
    @ProviderSessionId  VARCHAR (100),
    @RedirectUrl        NVARCHAR (500)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [Pay].[Payment]
    SET    [Status]            = 2,
           [ProviderSessionId] = @ProviderSessionId,
           [RedirectUrl]       = @RedirectUrl,
           [UpdatedOn]         = SYSUTCDATETIME()
    WHERE  [Id] = @PaymentId
      AND  [Status] = 1;
END;

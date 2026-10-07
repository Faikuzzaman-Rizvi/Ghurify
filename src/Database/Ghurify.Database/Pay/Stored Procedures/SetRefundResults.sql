-- Records the gateway's answers for a batch of refunds in one statement. Like SetRefundResult: a
-- refund already confirmed never changes again, and a failure counts the attempt.
CREATE PROCEDURE [Pay].[SetRefundResults]
    @Results [Pay].[RefundResultList] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [r]
    SET    [Status]            = CASE WHEN [x].[Succeeded] = 1 THEN 2 ELSE 3 END,
           [ProviderRefundRef] = COALESCE([x].[ProviderRefundRef], [r].[ProviderRefundRef]),
           [FailureReason]     = CASE WHEN [x].[Succeeded] = 1 THEN NULL ELSE [x].[FailureReason] END,
           [Attempts]          = CASE WHEN [r].[Attempts] < 255 THEN [r].[Attempts] + 1 ELSE [r].[Attempts] END,
           [CompletedOn]       = CASE WHEN [x].[Succeeded] = 1 THEN SYSUTCDATETIME() ELSE NULL END,
           [UpdatedOn]         = SYSUTCDATETIME()
    FROM   [Pay].[Refund] AS [r]
    JOIN   @Results       AS [x] ON [x].[RefundId] = [r].[Id]
    WHERE  [r].[Status] <> 2;
END;

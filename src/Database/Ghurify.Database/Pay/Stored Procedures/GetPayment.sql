-- One payment in full, as a receipt (a traveller's own) or for the admin desk:
--   @Audience 1 = a traveller: only a payment they made;
--   @Audience 3 = the admin desk: any payment.
-- Hosts never see a single payment's details here.
--
-- The three result sets always come back, empty when the payment is not visible to the viewer:
--   1. the payment, its booking, trip, host and traveller;
--   2. the refunds made against it;
--   3. the gateway callbacks received for it (admin desk only; empty for a traveller).
CREATE PROCEDURE [Pay].[GetPayment]
    @Audience   TINYINT,
    @ViewerId   BIGINT,
    @PaymentId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Visible BIT = 0, @TransactionRef VARCHAR (40);

    SELECT @Visible        = 1,
           @TransactionRef = [TransactionRef]
    FROM   [Pay].[Payment]
    WHERE  [Id] = @PaymentId
      AND  [Archived] = 0
      AND  ((@Audience = 1 AND [UserId] = @ViewerId) OR @Audience = 3);

    SELECT [p].[Id],
           [p].[BookingId],
           [b].[Status]       AS [BookingStatus],
           [b].[TripId],
           [t].[Title]        AS [TripTitle],
           [t].[StartDate],
           [t].[EndDate],
           [t].[HostId],
           [h].[DisplayName]  AS [HostName],
           [p].[UserId]       AS [TravellerId],
           [u].[DisplayName]  AS [TravellerName],
           [u].[Email]        AS [TravellerEmail],
           [p].[Provider],
           [p].[TransactionRef],
           [p].[ProviderTxnId],
           [p].[ValidationId],
           [p].[Status],
           [p].[FailureReason],
           [p].[MethodType],
           [p].[MethodName],
           [p].[AccountLast4],
           [p].[Issuer],
           [p].[Amount],
           [p].[Fee],
           [p].[Total],
           [p].[PaidAmount],
           [p].[StoreAmount],
           [p].[RiskFlagged],
           [p].[Currency],
           [p].[Created],
           [p].[CompletedOn],
           [p].[GatewayPaidOn]
    FROM   [Pay].[Payment] AS [p]
    JOIN   [Pay].[Booking] AS [b] ON [b].[Id] = [p].[BookingId]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    JOIN   [Main].[User]   AS [h] ON [h].[Id] = [t].[HostId]
    JOIN   [Main].[User]   AS [u] ON [u].[Id] = [p].[UserId]
    WHERE  [p].[Id] = @PaymentId
      AND  @Visible = 1;

    SELECT   [Id],
             [Amount],
             [Shortfall],
             [Reason],
             [Status],
             [ProviderRefundRef],
             [FailureReason],
             [Created],
             [CompletedOn]
    FROM     [Pay].[Refund]
    WHERE    [PaymentId] = @PaymentId
      AND    @Visible = 1
    ORDER BY [Id];

    SELECT   [Id],
             [EventId],
             [SignatureValid],
             [Outcome],
             [Created],
             [ProcessedOn]
    FROM     [Pay].[WebhookEvent]
    WHERE    [TransactionRef] = @TransactionRef
      AND    @Visible = 1
      AND    @Audience = 3
    ORDER BY [Id];
END;

-- Payment history, newest first, for three audiences. Who is asking decides what is visible, here
-- in SQL as well as in the use case:
--   @Audience 1 = a traveller: their own payment attempts, in every status;
--   @Audience 2 = a host: succeeded payments for trips they host (@ViewerId is the host);
--   @Audience 3 = the admin desk: every payment, searchable by payment or booking number, our
--                 transaction reference, the gateway's transaction or validation id, the
--                 traveller's email, or part of their name or the trip title.
-- @Status and @TripId narrow the list; @Search is only honoured for the admin desk.
--
--   1. one page of payments, each with what has been refunded against it, and the total count;
--   2. totals over everything visible with the same search and trip (but any status): how many
--      payments, how many succeeded, what was paid, the bookings' value and what was refunded.
CREATE PROCEDURE [Pay].[QueryPayments]
    @Audience  TINYINT,
    @ViewerId  BIGINT,
    @Status    TINYINT        = NULL,
    @TripId    BIGINT         = NULL,
    @Search    NVARCHAR (200) = NULL,
    @Offset    INT,
    @PageSize  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Term NVARCHAR (200) = CASE WHEN @Audience = 3 THEN NULLIF(LTRIM(RTRIM(@Search)), N'') END;
    DECLARE @Number BIGINT = TRY_CAST(@Term AS BIGINT);
    -- References are ASCII; comparing as VARCHAR keeps the lookups on their indexes.
    DECLARE @Reference VARCHAR (100) = CAST(@Term AS VARCHAR (100));
    DECLARE @Email NVARCHAR (256) = LOWER(@Term);

    SELECT   [p].[Id],
             [p].[BookingId],
             [b].[TripId],
             [t].[Title]        AS [TripTitle],
             [p].[UserId]       AS [TravellerId],
             [u].[DisplayName]  AS [TravellerName],
             [p].[Provider],
             [p].[TransactionRef],
             [p].[ProviderTxnId],
             [p].[Status],
             [p].[MethodType],
             [p].[MethodName],
             [p].[AccountLast4],
             [p].[Amount],
             [p].[Fee],
             [p].[Total],
             [p].[PaidAmount],
             [p].[Currency],
             ISNULL([r].[Refunded], 0) AS [Refunded],
             [p].[Created],
             [p].[CompletedOn],
             COUNT(1) OVER () AS [TotalCount]
    FROM     [Pay].[Payment] AS [p]
    JOIN     [Pay].[Booking] AS [b] ON [b].[Id] = [p].[BookingId]
    JOIN     [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    JOIN     [Main].[User]   AS [u] ON [u].[Id] = [p].[UserId]
    OUTER APPLY (SELECT SUM([Amount]) AS [Refunded]
                 FROM   [Pay].[Refund]
                 WHERE  [PaymentId] = [p].[Id]) AS [r]
    WHERE    [p].[Archived] = 0
      AND    ((@Audience = 1 AND [p].[UserId] = @ViewerId)
              OR (@Audience = 2 AND [t].[HostId] = @ViewerId AND [p].[Status] = 3)
              OR (@Audience = 3))
      AND    (@Status IS NULL OR [p].[Status] = @Status)
      AND    (@TripId IS NULL OR [b].[TripId] = @TripId)
      AND    (@Term IS NULL
              OR [p].[Id] = @Number
              OR [p].[BookingId] = @Number
              OR [p].[TransactionRef] = @Reference
              OR [p].[ProviderTxnId] = @Reference
              OR [p].[ValidationId] = @Reference
              OR [u].[Email] = @Email
              OR [u].[DisplayName] LIKE N'%' + @Term + N'%'
              OR [t].[Title] LIKE N'%' + @Term + N'%')
    ORDER BY [p].[Id] DESC
    OFFSET   @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);

    SELECT COUNT(1)                                                          AS [Count],
           ISNULL(SUM(CASE WHEN [p].[Status] = 3 THEN 1 END), 0)              AS [Succeeded],
           ISNULL(SUM(CASE WHEN [p].[Status] = 3 THEN [p].[PaidAmount] END), 0) AS [Paid],
           ISNULL(SUM(CASE WHEN [p].[Status] = 3 THEN [p].[Amount] END), 0)     AS [BookingValue],
           ISNULL(SUM([r].[Refunded]), 0)                                     AS [Refunded]
    FROM   [Pay].[Payment] AS [p]
    JOIN   [Pay].[Booking] AS [b] ON [b].[Id] = [p].[BookingId]
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    JOIN   [Main].[User]   AS [u] ON [u].[Id] = [p].[UserId]
    OUTER APPLY (SELECT SUM([Amount]) AS [Refunded]
                 FROM   [Pay].[Refund]
                 WHERE  [PaymentId] = [p].[Id]) AS [r]
    WHERE  [p].[Archived] = 0
      AND  ((@Audience = 1 AND [p].[UserId] = @ViewerId)
            OR (@Audience = 2 AND [t].[HostId] = @ViewerId AND [p].[Status] = 3)
            OR (@Audience = 3))
      AND  (@TripId IS NULL OR [b].[TripId] = @TripId)
      AND  (@Term IS NULL
            OR [p].[Id] = @Number
            OR [p].[BookingId] = @Number
            OR [p].[TransactionRef] = @Reference
            OR [p].[ProviderTxnId] = @Reference
            OR [p].[ValidationId] = @Reference
            OR [u].[Email] = @Email
            OR [u].[DisplayName] LIKE N'%' + @Term + N'%'
            OR [t].[Title] LIKE N'%' + @Term + N'%')
    OPTION (RECOMPILE);
END;

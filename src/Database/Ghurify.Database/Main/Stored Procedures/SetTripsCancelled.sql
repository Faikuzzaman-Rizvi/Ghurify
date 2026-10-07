-- Cancels whole trips at once: a host cancelling their own trip (@HostId set, so only their trips are
-- touched), or the safety desk closing a destination (@HostId NULL). One transaction, set-based:
--   - each trip still open (Draft, Published, Full) becomes Cancelled;
--   - unpaid (Held) seats are released and their pending payment attempts expired;
--   - paid (Confirmed) seats become Refunded; the caller creates the refunds from the first result;
--   - pending requests are declined, approved ones withdrawn.
--
-- Result 1: the paid bookings to refund in full (BookingId, UserId, TripId, Paid = what entered
--           escrow for that booking).
-- Result 2: everyone to tell (UserId, TripId), travellers and requesters alike.
-- Result 3: the trips that were cancelled by this call (TripId, HostId, Title).
-- Trips already cancelled or completed are left alone, so calling this twice changes nothing.
CREATE PROCEDURE [Main].[SetTripsCancelled]
    @TripIds  [Main].[IdList] READONLY,
    @HostId   BIGINT = NULL,
    @ActorId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Trips TABLE ([TripId] BIGINT NOT NULL PRIMARY KEY, [HostId] BIGINT NOT NULL, [Title] NVARCHAR (150) NOT NULL);
    DECLARE @Paid TABLE ([BookingId] BIGINT NOT NULL PRIMARY KEY, [UserId] BIGINT NOT NULL, [TripId] BIGINT NOT NULL);
    DECLARE @Notify TABLE ([UserId] BIGINT NOT NULL, [TripId] BIGINT NOT NULL, PRIMARY KEY ([UserId], [TripId]));

    BEGIN TRAN;

    UPDATE [t]
    SET    [Status]     = 4,
           [SeatsTaken] = 0,
           [UpdatedOn]  = SYSUTCDATETIME(),
           [UpdatedId]  = @ActorId
    OUTPUT inserted.[Id], inserted.[HostId], inserted.[Title] INTO @Trips ([TripId], [HostId], [Title])
    FROM   [Main].[Trip] AS [t] WITH (UPDLOCK, HOLDLOCK)
    JOIN   @TripIds      AS [i] ON [i].[Id] = [t].[Id]
    WHERE  [t].[Status] IN (1, 2, 3)
      AND  [t].[Archived] = 0
      AND  (@HostId IS NULL OR [t].[HostId] = @HostId);

    INSERT INTO @Notify ([UserId], [TripId])
    SELECT DISTINCT [r].[UserId], [r].[TripId]
    FROM   [Main].[JoinRequest] AS [r]
    JOIN   @Trips               AS [x] ON [x].[TripId] = [r].[TripId]
    WHERE  [r].[Status] IN (1, 2)
      AND  [r].[Archived] = 0;

    UPDATE [b]
    SET    [Status]      = 4,
           [CancelledOn] = SYSUTCDATETIME(),
           [UpdatedOn]   = SYSUTCDATETIME(),
           [UpdatedId]   = @ActorId
    OUTPUT inserted.[Id], inserted.[UserId], inserted.[TripId] INTO @Paid ([BookingId], [UserId], [TripId])
    FROM   [Pay].[Booking] AS [b] WITH (UPDLOCK, HOLDLOCK)
    JOIN   @Trips          AS [x] ON [x].[TripId] = [b].[TripId]
    WHERE  [b].[Status] = 2;

    UPDATE [p]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME()
    FROM   [Pay].[Payment] AS [p]
    JOIN   [Pay].[Booking] AS [b] ON [b].[Id] = [p].[BookingId]
    JOIN   @Trips          AS [x] ON [x].[TripId] = [b].[TripId]
    WHERE  [b].[Status] = 1
      AND  [p].[Status] IN (1, 2);

    UPDATE [b]
    SET    [Status]      = 3,
           [CancelledOn] = SYSUTCDATETIME(),
           [UpdatedOn]   = SYSUTCDATETIME(),
           [UpdatedId]   = @ActorId
    FROM   [Pay].[Booking] AS [b]
    JOIN   @Trips          AS [x] ON [x].[TripId] = [b].[TripId]
    WHERE  [b].[Status] = 1;

    UPDATE [r]
    SET    [Status]    = CASE WHEN [r].[Status] = 1 THEN 3 ELSE 5 END,
           [DecidedOn] = ISNULL([r].[DecidedOn], SYSUTCDATETIME()),
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @ActorId
    FROM   [Main].[JoinRequest] AS [r]
    JOIN   @Trips               AS [x] ON [x].[TripId] = [r].[TripId]
    WHERE  [r].[Status] IN (1, 2);

    COMMIT TRAN;

    SELECT [p].[BookingId],
           [p].[UserId],
           [p].[TripId],
           ISNULL((SELECT SUM([l].[Amount])
                   FROM   [Pay].[EscrowLedger] AS [l]
                   WHERE  [l].[BookingId] = [p].[BookingId]
                     AND  [l].[EntryType] = 1), 0) AS [Paid]
    FROM   @Paid AS [p];

    SELECT [UserId], [TripId] FROM @Notify;

    SELECT [TripId], [HostId], [Title] FROM @Trips;
END;

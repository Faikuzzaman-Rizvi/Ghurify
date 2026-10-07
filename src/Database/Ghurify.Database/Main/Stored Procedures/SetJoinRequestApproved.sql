-- The host approves a request: one seat is reserved and a Held booking is created with a payment
-- deadline, all in one transaction.
--
-- The seat is taken by a single guarded UPDATE (SeatsTaken < Seats). Two approvals racing for the
-- last seat serialise on the trip row: the first takes it, the second finds the trip full and
-- writes nothing. Seats can never go below zero or above the total (CK_Trip_Seats backs this up).
-- Filtered by @HostId, so only the trip's own host can approve.
--
-- @Result 0 = approved, 1 = not found (or not this host's trip), 2 = not pending,
-- 3 = no seats left, 4 = the trip is no longer live.
CREATE PROCEDURE [Main].[SetJoinRequestApproved]
    @RequestId      BIGINT,
    @HostId         BIGINT,
    @HoldExpiresAt  DATETIME2 (0),
    @BookingId      BIGINT  OUTPUT,
    @TravelerId     BIGINT  OUTPUT,
    @TripId         BIGINT  OUTPUT,
    @Result         TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @BookingId = NULL;
    SET @TravelerId = NULL;
    SET @TripId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @RequestStatus TINYINT;

    SELECT @TravelerId    = [r].[UserId],
           @TripId        = [r].[TripId],
           @RequestStatus = [r].[Status]
    FROM   [Main].[JoinRequest] AS [r] WITH (UPDLOCK, HOLDLOCK)
    JOIN   [Main].[Trip]        AS [t] ON [t].[Id] = [r].[TripId]
    WHERE  [r].[Id] = @RequestId
      AND  [r].[Archived] = 0
      AND  [t].[HostId] = @HostId;

    IF @RequestStatus IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @RequestStatus <> 1
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @Price DECIMAL (18, 2);

    UPDATE [Main].[Trip]
    SET    @Price       = [PricePerPerson],
           [SeatsTaken] = [SeatsTaken] + 1,
           [Status]     = CASE WHEN [SeatsTaken] + 1 >= [Seats] THEN 3 ELSE [Status] END,
           [UpdatedOn]  = SYSUTCDATETIME(),
           [UpdatedId]  = @HostId
    WHERE  [Id] = @TripId
      AND  [HostId] = @HostId
      AND  [Status] = 2
      AND  [SeatsTaken] < [Seats];

    IF @@ROWCOUNT = 0
    BEGIN
        SET @Result = CASE WHEN EXISTS (SELECT 1 FROM [Main].[Trip] WHERE [Id] = @TripId AND [Status] IN (2, 3))
                           THEN 3 ELSE 4 END;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Pay].[Booking] ([TripId], [UserId], [JoinRequestId], [Amount], [Status], [HoldExpiresAt], [UpdatedId])
    VALUES (@TripId, @TravelerId, @RequestId, @Price, 1, @HoldExpiresAt, @HostId);

    SET @BookingId = SCOPE_IDENTITY();

    UPDATE [Main].[JoinRequest]
    SET    [Status]    = 2,
           [DecidedOn] = SYSUTCDATETIME(),
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @HostId
    WHERE  [Id] = @RequestId;

    SET @Result = 0;

    COMMIT TRAN;
END;

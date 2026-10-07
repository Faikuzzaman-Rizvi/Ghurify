-- A traveller withdraws their own request before paying: a pending request is cancelled; an
-- approved one with an unpaid (Held) booking is cancelled and its seat handed back, in one
-- transaction. Filtered by @UserId: travellers can only withdraw their own.
-- A paid booking is not cancelled here; that goes through the refund rules.
--
-- @Result 0 = cancelled, 1 = not found (or not theirs), 2 = already closed,
-- 3 = already paid (use the booking cancellation, which refunds).
CREATE PROCEDURE [Main].[SetJoinRequestCancelled]
    @RequestId  BIGINT,
    @UserId     BIGINT,
    @HostId     BIGINT  OUTPUT,
    @TripId     BIGINT  OUTPUT,
    @Result     TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @HostId = NULL;
    SET @TripId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @BookingId BIGINT, @BookingStatus TINYINT;

    SELECT @Status = [r].[Status],
           @TripId = [r].[TripId],
           @HostId = [t].[HostId]
    FROM   [Main].[JoinRequest] AS [r] WITH (UPDLOCK, HOLDLOCK)
    JOIN   [Main].[Trip]        AS [t] ON [t].[Id] = [r].[TripId]
    WHERE  [r].[Id] = @RequestId
      AND  [r].[UserId] = @UserId
      AND  [r].[Archived] = 0;

    IF @Status IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status NOT IN (1, 2)
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    SELECT @BookingId     = [Id],
           @BookingStatus = [Status]
    FROM   [Pay].[Booking] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [JoinRequestId] = @RequestId;

    IF @BookingStatus = 2
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF @BookingStatus = 1
    BEGIN
        UPDATE [Pay].[Booking]
        SET    [Status]      = 3,
               [CancelledOn] = SYSUTCDATETIME(),
               [UpdatedOn]   = SYSUTCDATETIME(),
               [UpdatedId]   = @UserId
        WHERE  [Id] = @BookingId;

        UPDATE [Main].[Trip]
        SET    [SeatsTaken] = [SeatsTaken] - 1,
               [Status]     = CASE WHEN [Status] = 3 THEN 2 ELSE [Status] END,
               [UpdatedOn]  = SYSUTCDATETIME(),
               [UpdatedId]  = @UserId
        WHERE  [Id] = @TripId
          AND  [SeatsTaken] > 0;
    END;

    UPDATE [Main].[JoinRequest]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @UserId
    WHERE  [Id] = @RequestId;

    SET @Result = 0;

    COMMIT TRAN;
END;

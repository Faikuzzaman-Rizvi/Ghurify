-- A traveller cancels their own paid booking before the trip starts: the booking is closed
-- (Refunded when money goes back, Cancelled when the rules give none), the request withdrawn, and
-- the seat handed back to the trip, in one transaction. The refund itself is created by the caller
-- straight after, with the amount the rules give.
--
-- @Result 0 = cancelled, 1 = not found (or not theirs), 2 = not a paid booking, 3 = the trip has
-- already started or ended.
CREATE PROCEDURE [Pay].[SetBookingCancelled]
    @BookingId    BIGINT,
    @UserId       BIGINT,
    @Today        DATE,
    @WithRefund   BIT,
    @HostId       BIGINT OUTPUT,
    @TripId       BIGINT OUTPUT,
    @Result       TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @RequestId BIGINT, @StartDate DATE;

    SELECT @Status    = [b].[Status],
           @RequestId = [b].[JoinRequestId],
           @TripId    = [b].[TripId],
           @HostId    = [t].[HostId],
           @StartDate = [t].[StartDate]
    FROM   [Pay].[Booking] AS [b] WITH (UPDLOCK, HOLDLOCK)
    JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    WHERE  [b].[Id] = @BookingId
      AND  [b].[UserId] = @UserId
      AND  [b].[Archived] = 0;

    IF @Status IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status <> 2
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @StartDate <= @Today
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Pay].[Booking]
    SET    [Status]      = CASE WHEN @WithRefund = 1 THEN 4 ELSE 3 END,
           [CancelledOn] = SYSUTCDATETIME(),
           [UpdatedOn]   = SYSUTCDATETIME(),
           [UpdatedId]   = @UserId
    WHERE  [Id] = @BookingId;

    UPDATE [Main].[JoinRequest]
    SET    [Status]    = 5,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @UserId
    WHERE  [Id] = @RequestId;

    UPDATE [Main].[Trip]
    SET    [SeatsTaken] = [SeatsTaken] - 1,
           [Status]     = CASE WHEN [Status] = 3 THEN 2 ELSE [Status] END,
           [UpdatedOn]  = SYSUTCDATETIME()
    WHERE  [Id] = @TripId
      AND  [SeatsTaken] > 0;

    SET @Result = 0;

    COMMIT TRAN;
END;

-- The host declines a pending request. Filtered by @HostId: only the trip's own host can.
--
-- @Result 0 = declined, 1 = not found (or not this host's trip), 2 = not pending.
CREATE PROCEDURE [Main].[SetJoinRequestDeclined]
    @RequestId   BIGINT,
    @HostId      BIGINT,
    @TravelerId  BIGINT  OUTPUT,
    @TripId      BIGINT  OUTPUT,
    @Result      TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @TravelerId = NULL;
    SET @TripId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT;

    SELECT @TravelerId = [r].[UserId],
           @TripId     = [r].[TripId],
           @Status     = [r].[Status]
    FROM   [Main].[JoinRequest] AS [r] WITH (UPDLOCK, HOLDLOCK)
    JOIN   [Main].[Trip]        AS [t] ON [t].[Id] = [r].[TripId]
    WHERE  [r].[Id] = @RequestId
      AND  [r].[Archived] = 0
      AND  [t].[HostId] = @HostId;

    IF @Status IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status <> 1
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[JoinRequest]
    SET    [Status]    = 3,
           [DecidedOn] = SYSUTCDATETIME(),
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @HostId
    WHERE  [Id] = @RequestId;

    SET @Result = 0;

    COMMIT TRAN;
END;

-- A traveller asks to join a live trip.
--
-- The "one live request per traveller per trip" check and the insert are one atomic step, so a
-- double click cannot create two requests (the unique index would also refuse the second).
-- Eligibility that depends on who the traveller is (verified, women-only) is checked by the
-- caller; this procedure checks the trip itself.
--
-- @Result 0 = added, 1 = trip not found or not open to requests, 2 = it is the caller's own trip,
-- 3 = no seats left, 4 = the caller already has a live request for this trip.
CREATE PROCEDURE [Main].[AddJoinRequest]
    @TripId   BIGINT,
    @UserId   BIGINT,
    @Message  NVARCHAR (500),
    @Id       BIGINT  OUTPUT,
    @HostId   BIGINT  OUTPUT,
    @Result   TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @HostId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @Seats SMALLINT, @SeatsTaken SMALLINT, @DestinationStatus TINYINT;

    SELECT @HostId            = [t].[HostId],
           @Status            = [t].[Status],
           @Seats             = [t].[Seats],
           @SeatsTaken        = [t].[SeatsTaken],
           @DestinationStatus = [d].[Status]
    FROM   [Main].[Trip]        AS [t]
    JOIN   [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    WHERE  [t].[Id] = @TripId
      AND  [t].[Archived] = 0;

    -- A closed destination takes no new bookings, even before its trips are cancelled.
    IF @Status IS NULL OR @Status NOT IN (2, 3) OR @DestinationStatus = 3
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @HostId = @UserId
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status = 3 OR @SeatsTaken >= @Seats
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF EXISTS (SELECT 1
               FROM   [Main].[JoinRequest] WITH (UPDLOCK, HOLDLOCK)
               WHERE  [TripId] = @TripId
                 AND  [UserId] = @UserId
                 AND  [Status] IN (1, 2)
                 AND  [Archived] = 0)
    BEGIN
        SET @Result = 4;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Main].[JoinRequest] ([TripId], [UserId], [Message], [UpdatedId])
    VALUES (@TripId, @UserId, @Message, @UserId);

    SET @Id = SCOPE_IDENTITY();
    SET @Result = 0;

    COMMIT TRAN;
END;

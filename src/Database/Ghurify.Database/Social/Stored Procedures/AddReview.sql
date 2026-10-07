-- Records a review after a completed trip and updates the reviewee's rating summary, as one
-- transaction.
--
-- Only people who were on the trip take part: the host, and travellers whose seat was paid
-- (a booking Confirmed when the trip completed). Direction must match who is who:
-- 1 traveller -> host, 2 host -> traveller. (3 traveller -> guide needs guides on trips, which the
-- marketplace adds; until then it is refused.)
-- One review per reviewer, reviewee and direction per trip (the unique index backs this up).
--
-- @Result 0 = added, 1 = no such trip, 2 = the trip is not completed, 3 = the reviewer was not on
-- the trip, 4 = the reviewee was not on the trip in that role, 5 = already reviewed.
CREATE PROCEDURE [Social].[AddReview]
    @TripId      BIGINT,
    @ReviewerId  BIGINT,
    @RevieweeId  BIGINT,
    @Direction   TINYINT,
    @Rating      TINYINT,
    @Body        NVARCHAR (1000),
    @Id          BIGINT  OUTPUT,
    @Result      TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @HostId BIGINT, @Status TINYINT;

    SELECT @HostId = [HostId], @Status = [Status]
    FROM   [Main].[Trip]
    WHERE  [Id] = @TripId
      AND  [Archived] = 0;

    IF @HostId IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status <> 5
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @ReviewerTravelled BIT = CASE WHEN EXISTS (SELECT 1 FROM [Pay].[Booking]
                                                       WHERE [TripId] = @TripId AND [UserId] = @ReviewerId AND [Status] = 2)
                                          THEN 1 ELSE 0 END;
    DECLARE @RevieweeTravelled BIT = CASE WHEN EXISTS (SELECT 1 FROM [Pay].[Booking]
                                                       WHERE [TripId] = @TripId AND [UserId] = @RevieweeId AND [Status] = 2)
                                          THEN 1 ELSE 0 END;

    IF NOT ((@Direction = 1 AND @ReviewerTravelled = 1) OR (@Direction = 2 AND @ReviewerId = @HostId))
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF NOT ((@Direction = 1 AND @RevieweeId = @HostId) OR (@Direction = 2 AND @RevieweeTravelled = 1))
    BEGIN
        SET @Result = 4;
        COMMIT TRAN;
        RETURN;
    END;

    IF EXISTS (SELECT 1
               FROM   [Social].[Review] WITH (UPDLOCK, HOLDLOCK)
               WHERE  [TripId] = @TripId
                 AND  [ReviewerId] = @ReviewerId
                 AND  [RevieweeId] = @RevieweeId
                 AND  [Direction] = @Direction)
    BEGIN
        SET @Result = 5;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Social].[Review] ([TripId], [ReviewerId], [RevieweeId], [Direction], [Rating], [Body], [UpdatedId])
    VALUES (@TripId, @ReviewerId, @RevieweeId, @Direction, @Rating, @Body, @ReviewerId);

    SET @Id = SCOPE_IDENTITY();

    -- Recompute the reviewee's summary from their reviews: always exact, never drifting.
    DECLARE @HostCount INT, @HostAverage DECIMAL (3, 2), @TravelerCount INT, @TravelerAverage DECIMAL (3, 2);

    SELECT @HostCount       = SUM(CASE WHEN [Direction] = 1 THEN 1 ELSE 0 END),
           @HostAverage     = CAST(AVG(CASE WHEN [Direction] = 1 THEN CAST([Rating] AS DECIMAL (5, 2)) END) AS DECIMAL (3, 2)),
           @TravelerCount   = SUM(CASE WHEN [Direction] = 2 THEN 1 ELSE 0 END),
           @TravelerAverage = CAST(AVG(CASE WHEN [Direction] = 2 THEN CAST([Rating] AS DECIMAL (5, 2)) END) AS DECIMAL (3, 2))
    FROM   [Social].[Review]
    WHERE  [RevieweeId] = @RevieweeId
      AND  [Archived] = 0;

    UPDATE [Social].[RatingSummary] WITH (UPDLOCK, HOLDLOCK)
    SET    [AsHostCount]       = ISNULL(@HostCount, 0),
           [AsHostAverage]     = @HostAverage,
           [AsTravelerCount]   = ISNULL(@TravelerCount, 0),
           [AsTravelerAverage] = @TravelerAverage,
           [UpdatedOn]         = SYSUTCDATETIME()
    WHERE  [UserId] = @RevieweeId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Social].[RatingSummary] ([UserId], [AsHostCount], [AsHostAverage], [AsTravelerCount], [AsTravelerAverage])
        VALUES (@RevieweeId, ISNULL(@HostCount, 0), @HostAverage, ISNULL(@TravelerCount, 0), @TravelerAverage);
    END;

    SET @Result = 0;

    COMMIT TRAN;
END;

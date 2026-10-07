-- Who the signed-in user may review on a completed trip, and whether they already have:
-- a traveller reviews the host; the host reviews each traveller with a paid seat.
-- Empty when the trip is not completed or the user was not on it.
CREATE PROCEDURE [Social].[QueryReviewable]
    @TripId  BIGINT,
    @UserId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HostId BIGINT, @Status TINYINT;
    SELECT @HostId = [HostId], @Status = [Status] FROM [Main].[Trip] WHERE [Id] = @TripId AND [Archived] = 0;

    IF @Status IS NULL OR @Status <> 5
    BEGIN
        SELECT CAST(NULL AS BIGINT) AS [UserId], CAST(NULL AS NVARCHAR (100)) AS [DisplayName],
               CAST(NULL AS TINYINT) AS [Direction], CAST(NULL AS BIT) AS [AlreadyReviewed]
        WHERE  1 = 0;
        RETURN;
    END;

    -- A paid traveller reviews the host.
    SELECT [h].[Id] AS [UserId], [h].[DisplayName], CAST(1 AS TINYINT) AS [Direction],
           CAST(CASE WHEN EXISTS (SELECT 1 FROM [Social].[Review] AS [r]
                                  WHERE [r].[TripId] = @TripId AND [r].[ReviewerId] = @UserId
                                    AND [r].[RevieweeId] = [h].[Id] AND [r].[Direction] = 1)
                     THEN 1 ELSE 0 END AS BIT) AS [AlreadyReviewed]
    FROM   [Main].[User] AS [h]
    WHERE  [h].[Id] = @HostId
      AND  EXISTS (SELECT 1 FROM [Pay].[Booking] AS [b]
                   WHERE [b].[TripId] = @TripId AND [b].[UserId] = @UserId AND [b].[Status] = 2)
    UNION ALL
    -- The host reviews each paid traveller.
    SELECT [t].[Id], [t].[DisplayName], CAST(2 AS TINYINT),
           CAST(CASE WHEN EXISTS (SELECT 1 FROM [Social].[Review] AS [r]
                                  WHERE [r].[TripId] = @TripId AND [r].[ReviewerId] = @UserId
                                    AND [r].[RevieweeId] = [t].[Id] AND [r].[Direction] = 2)
                     THEN 1 ELSE 0 END AS BIT)
    FROM   [Pay].[Booking] AS [b]
    JOIN   [Main].[User]   AS [t] ON [t].[Id] = [b].[UserId]
    WHERE  [b].[TripId] = @TripId
      AND  [b].[Status] = 2
      AND  @UserId = @HostId;
END;

-- Puts someone's own uploaded photos on one of their travel-map visits. Only images they uploaded,
-- not on a story or another visit, and not failed; at most @MaxPhotos on one visit, counting those
-- still being processed. The OwnerId filter stops anyone attaching someone else's photo, and the
-- visit's UserId filter stops anyone using someone else's visit.
--
-- Called on its own, and by AddVisit inside its transaction (a new place with its photos).
--
-- @Result 0 = attached, 1 = no such visit of theirs, 2 = a photo cannot be attached (nothing
-- written), 3 = the visit would have more than @MaxPhotos photos.
CREATE PROCEDURE [Main].[AddVisitPhotos]
    @UserId     BIGINT,
    @VisitId    BIGINT,
    @MediaIds   [Main].[IdList] READONLY,
    @MaxPhotos  INT,
    @Result     TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 0;

    BEGIN TRAN;

    -- Locks the visit, so two uploads at once cannot both slip under the limit.
    IF NOT EXISTS (SELECT 1
                   FROM   [Main].[Visit] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [Id] = @VisitId
                     AND  [UserId] = @UserId
                     AND  [Archived] = 0)
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @Requested INT = (SELECT COUNT(1) FROM @MediaIds);
    DECLARE @Usable INT =
    (
        SELECT COUNT(1)
        FROM   [Social].[Media] AS [m] WITH (UPDLOCK, HOLDLOCK)
        JOIN   @MediaIds        AS [i] ON [i].[Id] = [m].[Id]
        WHERE  [m].[OwnerId] = @UserId
          AND  [m].[PostId] IS NULL
          AND  [m].[VisitId] IS NULL
          AND  [m].[Kind] = 1
          AND  [m].[Status] IN (2, 3)
          AND  [m].[Archived] = 0
    );

    IF @Usable <> @Requested
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF (SELECT COUNT(1)
        FROM   [Social].[Media]
        WHERE  [VisitId] = @VisitId
          AND  [Status] IN (2, 3)
          AND  [Archived] = 0) + @Requested > @MaxPhotos
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [m]
    SET    [VisitId]   = @VisitId,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @UserId
    FROM   [Social].[Media] AS [m]
    JOIN   @MediaIds        AS [i] ON [i].[Id] = [m].[Id]
    WHERE  [m].[OwnerId] = @UserId
      AND  [m].[PostId] IS NULL
      AND  [m].[VisitId] IS NULL;

    COMMIT TRAN;
END;

-- Someone adds a place they have been to their travel map: a Ghurify destination (@DestinationSlug),
-- or anywhere else as a named point (@PlaceName, @Division, @Latitude, @Longitude). The caller has
-- already checked the point is in Bangladesh and the date is not in the future.
--
-- Photos they uploaded for it (@MediaIds) go on the new visit in the same transaction: the place
-- and its photos are added together or not at all.
--
-- @Result 0 = added, 1 = no such destination, 2 = that place is already on their map for that day
-- (including from a Ghurify trip), 3 = they have added as many places as allowed (@MaxAdded),
-- 4 = a photo cannot be attached, 5 = more than @MaxPhotos photos.
CREATE PROCEDURE [Main].[AddVisit]
    @UserId           BIGINT,
    @DestinationSlug  VARCHAR (60)    = NULL,
    @PlaceName        NVARCHAR (120)  = NULL,
    @Division         TINYINT         = NULL,
    @Latitude         DECIMAL (9, 6)  = NULL,
    @Longitude        DECIMAL (9, 6)  = NULL,
    @VisitedOn        DATE,
    @Note             NVARCHAR (500)  = NULL,
    @MaxAdded         INT,
    @MediaIds         [Main].[IdList] READONLY,
    @MaxPhotos        INT,
    @Id               BIGINT  OUTPUT,
    @Result           TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @Result = 0;

    DECLARE @DestinationId BIGINT = NULL;

    IF @DestinationSlug IS NOT NULL
    BEGIN
        SELECT @DestinationId = [Id]
        FROM   [Main].[Destination]
        WHERE  [Slug] = @DestinationSlug
          AND  [Archived] = 0;

        IF @DestinationId IS NULL
        BEGIN
            SET @Result = 1;
            RETURN;
        END;
    END;

    BEGIN TRAN;

    -- Locks this person's visits, so two adds at once cannot both slip under the limit.
    IF (SELECT COUNT(1)
        FROM   [Main].[Visit] WITH (UPDLOCK, HOLDLOCK)
        WHERE  [UserId] = @UserId
          AND  [Source] = 2
          AND  [Archived] = 0) >= @MaxAdded
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF EXISTS (SELECT 1
               FROM   [Main].[Visit]
               WHERE  [UserId] = @UserId
                 AND  [Archived] = 0
                 AND  [VisitedOn] = @VisitedOn
                 AND  ((@DestinationId IS NOT NULL AND [DestinationId] = @DestinationId)
                       OR (@DestinationId IS NULL AND [DestinationId] IS NULL AND [PlaceName] = @PlaceName)))
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Main].[Visit]
           ([UserId], [Source], [DestinationId], [PlaceName], [Division], [Location], [VisitedOn], [Note], [UpdatedId])
    VALUES (@UserId,
            2,
            @DestinationId,
            CASE WHEN @DestinationId IS NULL THEN @PlaceName END,
            CASE WHEN @DestinationId IS NULL THEN @Division END,
            CASE WHEN @DestinationId IS NULL THEN geography::Point(@Latitude, @Longitude, 4326) END,
            @VisitedOn,
            @Note,
            @UserId);

    SET @Id = SCOPE_IDENTITY();

    IF EXISTS (SELECT 1 FROM @MediaIds)
    BEGIN
        DECLARE @Photos TINYINT;
        EXEC [Main].[AddVisitPhotos]
             @UserId    = @UserId,
             @VisitId   = @Id,
             @MediaIds  = @MediaIds,
             @MaxPhotos = @MaxPhotos,
             @Result    = @Photos OUTPUT;

        IF @Photos <> 0
        BEGIN
            ROLLBACK TRAN;
            SET @Id = NULL;
            SET @Result = CASE WHEN @Photos = 3 THEN 5 ELSE 4 END;
            RETURN;
        END;
    END;

    COMMIT TRAN;
END;

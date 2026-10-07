-- Publishes a story and attaches the author's own uploaded media to it, in one transaction.
-- Media that is not the author's, already on another post, or failed is silently left out: the
-- filter on OwnerId is what stops anyone attaching someone else's photo.
--
-- @Result 0 = posted, 1 = a media item could not be attached (nothing written).
CREATE PROCEDURE [Social].[AddPost]
    @AuthorId       BIGINT,
    @Body           NVARCHAR (2000),
    @DestinationId  BIGINT = NULL,
    @TripId         BIGINT = NULL,
    @MediaIds       [Main].[IdList] READONLY,
    @Id             BIGINT  OUTPUT,
    @Result         TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @Result = 0;

    BEGIN TRAN;

    DECLARE @Requested INT = (SELECT COUNT(1) FROM @MediaIds);
    DECLARE @Usable INT =
    (
        SELECT COUNT(1)
        FROM   [Social].[Media] AS [m] WITH (UPDLOCK, HOLDLOCK)
        JOIN   @MediaIds        AS [i] ON [i].[Id] = [m].[Id]
        WHERE  [m].[OwnerId] = @AuthorId
          AND  [m].[PostId] IS NULL
          AND  [m].[Status] IN (2, 3)
          AND  [m].[Archived] = 0
    );

    IF @Usable <> @Requested
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Social].[Post] ([AuthorId], [DestinationId], [TripId], [Body], [UpdatedId])
    VALUES (@AuthorId, @DestinationId, @TripId, @Body, @AuthorId);

    SET @Id = SCOPE_IDENTITY();

    UPDATE [m]
    SET    [PostId]    = @Id,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @AuthorId
    FROM   [Social].[Media] AS [m]
    JOIN   @MediaIds        AS [i] ON [i].[Id] = [m].[Id]
    WHERE  [m].[OwnerId] = @AuthorId;

    COMMIT TRAN;
END;

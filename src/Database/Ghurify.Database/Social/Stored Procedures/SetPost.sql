-- The author edits their own story: the text, the destination, and which of its photos or videos
-- stay (@KeepMediaIds; the rest are archived). Only the author's own, live post is touched: the
-- filter on AuthorId is the ownership check. An edit that would leave neither text nor media is
-- refused, so a story never becomes empty.
--
-- @Result 0 = saved, 1 = no such post of theirs, 2 = it would be empty (nothing written).
CREATE PROCEDURE [Social].[SetPost]
    @PostId         BIGINT,
    @AuthorId       BIGINT,
    @Body           NVARCHAR (2000),
    @DestinationId  BIGINT = NULL,
    @KeepMediaIds   [Main].[IdList] READONLY,
    @Result         TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 0;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1
                   FROM   [Social].[Post] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [Id] = @PostId
                     AND  [AuthorId] = @AuthorId
                     AND  [Status] = 1
                     AND  [Archived] = 0)
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    -- Only ids that really are this post's media count; anything else in the list is ignored.
    DECLARE @Kept INT =
    (
        SELECT COUNT(1)
        FROM   [Social].[Media] AS [m]
        JOIN   @KeepMediaIds    AS [k] ON [k].[Id] = [m].[Id]
        WHERE  [m].[PostId] = @PostId
          AND  [m].[Archived] = 0
    );

    IF LEN(@Body) = 0 AND @Kept = 0
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Social].[Post]
    SET    [Body]          = @Body,
           [DestinationId] = @DestinationId,
           [EditedOn]      = SYSUTCDATETIME(),
           [UpdatedOn]     = SYSUTCDATETIME(),
           [UpdatedId]     = @AuthorId
    WHERE  [Id] = @PostId;

    UPDATE [m]
    SET    [Archived]  = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @AuthorId
    FROM   [Social].[Media] AS [m]
    WHERE  [m].[PostId] = @PostId
      AND  [m].[Archived] = 0
      AND  NOT EXISTS (SELECT 1 FROM @KeepMediaIds AS [k] WHERE [k].[Id] = [m].[Id]);

    COMMIT TRAN;
END;

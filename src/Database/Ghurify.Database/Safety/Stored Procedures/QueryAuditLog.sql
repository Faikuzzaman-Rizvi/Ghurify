-- The audit viewer: who did what, newest first, filtered and paged.
--
-- Every filter is optional and NULL means "no filter", so the one procedure serves the whole
-- screen and the per-entity history ("every decision about this payout") behind an entity.
-- @Total rides on each row, as the other admin searches do, so the pager needs no second call.
CREATE PROCEDURE [Safety].[QueryAuditLog]
    @ActorId     BIGINT        = NULL,
    -- Matches a whole action (payout.approve) or every action in a group when it ends with a
    -- dot (payout.), which is how the screen's "area" filter works.
    @Action      VARCHAR (60)  = NULL,
    @EntityType  VARCHAR (40)  = NULL,
    @EntityId    BIGINT        = NULL,
    @FromUtc     DATETIME2 (0) = NULL,
    @ToUtc       DATETIME2 (0) = NULL,
    @Page        INT           = 1,
    @PageSize    INT           = 50
AS
BEGIN
    SET NOCOUNT ON;

    SET @Page = IIF(@Page < 1, 1, @Page);
    SET @PageSize = IIF(@PageSize < 1 OR @PageSize > 200, 50, @PageSize);

    DECLARE @GroupPrefix VARCHAR (61) = NULL;
    IF @Action IS NOT NULL AND RIGHT(@Action, 1) = '.'
    BEGIN
        -- Escape the LIKE wildcards: an action group is a literal prefix, never a pattern.
        SET @GroupPrefix = REPLACE(REPLACE(REPLACE(@Action, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%';
        SET @Action = NULL;
    END;

    SELECT    [a].[Id],
              [a].[ActorId],
              [u].[DisplayName] AS [ActorName],
              [a].[Action],
              [a].[EntityType],
              [a].[EntityId],
              [a].[Note],
              [a].[Changes],
              [a].[Created],
              COUNT(1) OVER () AS [Total]
    FROM      [Safety].[AuditLog] AS [a]
    JOIN      [Main].[User]       AS [u] ON [u].[Id] = [a].[ActorId]
    WHERE     [a].[Archived] = 0
      AND     (@ActorId IS NULL OR [a].[ActorId] = @ActorId)
      AND     (@Action IS NULL OR [a].[Action] = @Action)
      AND     (@GroupPrefix IS NULL OR [a].[Action] LIKE @GroupPrefix)
      AND     (@EntityType IS NULL OR [a].[EntityType] = @EntityType)
      AND     (@EntityId IS NULL OR [a].[EntityId] = @EntityId)
      AND     (@FromUtc IS NULL OR [a].[Created] >= @FromUtc)
      AND     (@ToUtc IS NULL OR [a].[Created] < @ToUtc)
    ORDER BY  [a].[Created] DESC, [a].[Id] DESC
    OFFSET    (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

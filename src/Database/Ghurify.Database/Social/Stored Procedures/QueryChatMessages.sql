-- A page of a trip's chat, newest first, older than @BeforeId (or the newest page when NULL),
-- with the sender's name. A second result set carries the pinned announcements.
-- The caller has already checked that the reader is a member.
CREATE PROCEDURE [Social].[QueryChatMessages]
    @TripId    BIGINT,
    @BeforeId  BIGINT = NULL,
    @Take      INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   TOP (@Take)
             [m].[Id],
             [m].[TripId],
             [m].[SenderId],
             [u].[DisplayName] AS [SenderName],
             [m].[Kind],
             [m].[Body],
             [m].[IsPinned],
             [m].[WasMasked],
             [m].[Created]
    FROM     [Social].[ChatMessage] AS [m]
    JOIN     [Main].[User]          AS [u] ON [u].[Id] = [m].[SenderId]
    WHERE    [m].[TripId] = @TripId
      AND    [m].[Archived] = 0
      AND    (@BeforeId IS NULL OR [m].[Id] < @BeforeId)
    ORDER BY [m].[Id] DESC;

    SELECT   [m].[Id],
             [m].[TripId],
             [m].[SenderId],
             [u].[DisplayName] AS [SenderName],
             [m].[Kind],
             [m].[Body],
             [m].[IsPinned],
             [m].[WasMasked],
             [m].[Created]
    FROM     [Social].[ChatMessage] AS [m]
    JOIN     [Main].[User]          AS [u] ON [u].[Id] = [m].[SenderId]
    WHERE    [m].[TripId] = @TripId
      AND    [m].[IsPinned] = 1
      AND    [m].[Archived] = 0
    ORDER BY [m].[Id] DESC;
END;

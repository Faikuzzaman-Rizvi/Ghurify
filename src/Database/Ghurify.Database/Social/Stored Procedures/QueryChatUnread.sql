-- Unread chat messages for one user across every trip they are in (as host or with a held or paid
-- seat), counting only other people's messages after their read marker.
CREATE PROCEDURE [Social].[QueryChatUnread]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    WITH [Mine] AS
    (
        SELECT [t].[Id] AS [TripId], [t].[Title]
        FROM   [Main].[Trip] AS [t]
        WHERE  [t].[Archived] = 0
          AND  ([t].[HostId] = @UserId
                OR EXISTS (SELECT 1
                           FROM   [Pay].[Booking] AS [b]
                           WHERE  [b].[TripId] = [t].[Id]
                             AND  [b].[UserId] = @UserId
                             AND  [b].[Status] IN (1, 2)
                             AND  [b].[Archived] = 0))
    )
    SELECT [mine].[TripId],
           [mine].[Title],
           (SELECT COUNT(1)
            FROM   [Social].[ChatMessage] AS [m]
            WHERE  [m].[TripId] = [mine].[TripId]
              AND  [m].[SenderId] <> @UserId
              AND  [m].[Archived] = 0
              AND  [m].[Id] > ISNULL([r].[LastReadId], 0)) AS [Unread]
    FROM   [Mine] AS [mine]
    LEFT JOIN [Social].[ChatReadMarker] AS [r]
           ON [r].[TripId] = [mine].[TripId] AND [r].[UserId] = @UserId;
END;

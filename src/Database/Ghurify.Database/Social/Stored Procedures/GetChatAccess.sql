-- Whether a user is in a trip's group chat, and whether contact numbers must be masked.
--
-- Members: the trip's host, and travellers with a held or paid seat. HasUnpaidMembers is true while
-- any member's seat is only held: until everyone has paid into escrow, numbers are hidden.
-- An empty result means the trip does not exist (or is archived).
CREATE PROCEDURE [Social].[GetChatAccess]
    @TripId  BIGINT,
    @UserId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [t].[Id] AS [TripId],
           [t].[Title],
           CAST(CASE WHEN [t].[HostId] = @UserId THEN 1 ELSE 0 END AS BIT) AS [IsHost],
           CAST(CASE WHEN [t].[HostId] = @UserId
                       OR EXISTS (SELECT 1
                                  FROM   [Pay].[Booking] AS [b]
                                  WHERE  [b].[TripId] = [t].[Id]
                                    AND  [b].[UserId] = @UserId
                                    AND  [b].[Status] IN (1, 2)
                                    AND  [b].[Archived] = 0)
                     THEN 1 ELSE 0 END AS BIT) AS [IsMember],
           CAST(CASE WHEN EXISTS (SELECT 1
                                  FROM   [Pay].[Booking] AS [b]
                                  WHERE  [b].[TripId] = [t].[Id]
                                    AND  [b].[Status] = 1
                                    AND  [b].[Archived] = 0)
                     THEN 1 ELSE 0 END AS BIT) AS [HasUnpaidMembers]
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Id] = @TripId
      AND  [t].[Archived] = 0;
END;

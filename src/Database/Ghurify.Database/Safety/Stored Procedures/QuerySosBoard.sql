-- The safety desk's live board: every SOS not yet resolved (or, with @IncludeResolved, the last 50
-- of all), newest first, with who, where, which trip and its host.
CREATE PROCEDURE [Safety].[QuerySosBoard]
    @IncludeResolved BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   TOP (50)
             [s].[Id],
             [s].[UserId],
             [u].[DisplayName]  AS [UserName],
             [u].[Phone]        AS [UserPhone],
             [s].[TripId],
             [t].[Title]        AS [TripTitle],
             [h].[DisplayName]  AS [HostName],
             [h].[Phone]        AS [HostPhone],
             [s].[Latitude],
             [s].[Longitude],
             [s].[Message],
             [s].[Status],
             [s].[LastSeenOn],
             [s].[Created]
    FROM     [Safety].[SosEvent] AS [s]
    JOIN     [Main].[User]       AS [u] ON [u].[Id] = [s].[UserId]
    JOIN     [Main].[Trip]       AS [t] ON [t].[Id] = [s].[TripId]
    JOIN     [Main].[User]       AS [h] ON [h].[Id] = [t].[HostId]
    WHERE    [s].[Archived] = 0
      AND    (@IncludeResolved = 1 OR [s].[Status] IN (1, 2))
    ORDER BY [s].[Status] ASC, [s].[Id] DESC;
END;

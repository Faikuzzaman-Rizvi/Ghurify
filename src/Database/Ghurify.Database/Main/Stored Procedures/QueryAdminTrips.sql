-- The admin trip search, in every status: by id, or by any part of the title, the host's name or
-- the destination. Newest first.
CREATE PROCEDURE [Main].[QueryAdminTrips]
    @Search    NVARCHAR (200) = NULL,
    @Status    TINYINT        = NULL,
    @Offset    INT,
    @PageSize  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Term NVARCHAR (200) = NULLIF(LTRIM(RTRIM(@Search)), N'');
    DECLARE @Id BIGINT = TRY_CAST(@Term AS BIGINT);

    SELECT   [t].[Id],
             [t].[Title],
             [t].[HostId],
             [u].[DisplayName] AS [HostName],
             [d].[Name]        AS [DestinationName],
             [t].[StartDate],
             [t].[EndDate],
             [t].[Status],
             [t].[Seats],
             [t].[SeatsTaken],
             [t].[PricePerPerson],
             COUNT(1) OVER () AS [TotalCount]
    FROM     [Main].[Trip]        AS [t]
    JOIN     [Main].[User]        AS [u] ON [u].[Id] = [t].[HostId]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    WHERE    [t].[Archived] = 0
      AND    (@Status IS NULL OR [t].[Status] = @Status)
      AND    (@Term IS NULL
              OR [t].[Id] = @Id
              OR [t].[Title] LIKE N'%' + @Term + N'%'
              OR [u].[DisplayName] LIKE N'%' + @Term + N'%'
              OR [d].[Name] LIKE N'%' + @Term + N'%')
    ORDER BY [t].[Id] DESC
    OFFSET   @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

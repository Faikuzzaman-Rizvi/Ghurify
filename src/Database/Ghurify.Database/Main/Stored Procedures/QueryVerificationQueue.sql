-- The admin verification queue: checks in one status, oldest first, paged, with who asked.
-- Email is returned for masking by the caller; it is never sent to the browser in full.
CREATE PROCEDURE [Main].[QueryVerificationQueue]
    @Status    TINYINT,
    @Offset    INT,
    @PageSize  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [v].[Id],
             [v].[UserId],
             [u].[DisplayName],
             [u].[Email],
             [v].[Level],
             [v].[Status],
             [v].[Provider],
             [v].[ProviderRef],
             [v].[Reason],
             [v].[Created],
             [v].[IdType],
             (SELECT COUNT(1) FROM [Main].[VerificationDocument] AS [d]
              WHERE [d].[VerificationId] = [v].[Id] AND [d].[Status] = 2) AS [DocumentCount],
             COUNT(1) OVER () AS [TotalCount]
    FROM     [Main].[Verification] AS [v]
    JOIN     [Main].[User]         AS [u] ON [u].[Id] = [v].[UserId]
    WHERE    [v].[Status] = @Status
      AND    [v].[Archived] = 0
    ORDER BY [v].[Created] ASC, [v].[Id] ASC
    OFFSET   @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

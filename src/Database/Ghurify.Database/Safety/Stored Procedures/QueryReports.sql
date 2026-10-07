-- The moderation queue: reports of one kind (or all) in one status, oldest first, with who reported.
CREATE PROCEDURE [Safety].[QueryReports]
    @Kind    TINYINT = NULL,
    @Status  TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   TOP (100)
             [r].[Id],
             [r].[ReporterId],
             [u].[DisplayName] AS [ReporterName],
             [r].[Kind],
             [r].[TargetId],
             [r].[Reason],
             [r].[Details],
             [r].[Status],
             [r].[Resolution],
             [r].[Created]
    FROM     [Safety].[Report] AS [r]
    JOIN     [Main].[User]     AS [u] ON [u].[Id] = [r].[ReporterId]
    WHERE    [r].[Archived] = 0
      AND    [r].[Status] = @Status
      AND    (@Kind IS NULL OR [r].[Kind] = @Kind)
    ORDER BY [r].[Created] ASC, [r].[Id] ASC;
END;

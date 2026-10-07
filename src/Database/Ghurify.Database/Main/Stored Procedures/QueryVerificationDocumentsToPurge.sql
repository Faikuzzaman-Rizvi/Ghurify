-- Identity documents whose images must now be deleted:
--   * documents of a check that was decided (approved or rejected) before @DecidedBefore;
--   * uploads never submitted with a check, made before @AbandonedBefore.
-- At most @Take rows per call; the purge job pages through them.
CREATE PROCEDURE [Main].[QueryVerificationDocumentsToPurge]
    @DecidedBefore    DATETIME2 (0),
    @AbandonedBefore  DATETIME2 (0),
    @Take             INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Take)
           [d].[Id],
           [d].[UploadBlob],
           [d].[Blob]
    FROM   [Main].[VerificationDocument] AS [d]
    LEFT JOIN [Main].[Verification] AS [v] ON [v].[Id] = [d].[VerificationId]
    WHERE  [d].[Status] <> 4
      AND  (   ([v].[Id] IS NOT NULL AND [v].[Status] IN (2, 3) AND [v].[ReviewedOn] < @DecidedBefore)
            OR ([d].[VerificationId] IS NULL AND [d].[Created] < @AbandonedBefore))
    ORDER BY [d].[Id];
END;

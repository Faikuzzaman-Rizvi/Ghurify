-- Payouts: one host's own (@HostId), or every payout in a status for the admin desk (@HostId NULL,
-- @Status set). Newest first, with the trip each one is for.
CREATE PROCEDURE [Pay].[QueryHostPayouts]
    @HostId  BIGINT  = NULL,
    @Status  TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   TOP (200)
             [p].[Id],
             [p].[TripId],
             [t].[Title]        AS [TripTitle],
             [t].[StartDate],
             [p].[HostId],
             [h].[DisplayName]  AS [HostName],
             [p].[Stage],
             [p].[Amount],
             [p].[PlatformAmount],
             [p].[Status],
             [p].[Created],
             [p].[ApprovedOn]
    FROM     [Pay].[Payout] AS [p]
    JOIN     [Main].[Trip]  AS [t] ON [t].[Id] = [p].[TripId]
    JOIN     [Main].[User]  AS [h] ON [h].[Id] = [p].[HostId]
    WHERE    (@HostId IS NULL OR [p].[HostId] = @HostId)
      AND    (@Status IS NULL OR [p].[Status] = @Status)
      AND    [p].[Archived] = 0
    ORDER BY [p].[Id] DESC;
END;

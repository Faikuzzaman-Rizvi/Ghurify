-- The admin overview in one row: what needs a person's attention now, and how the platform is doing.
CREATE PROCEDURE [Safety].[GetDashboardCounts]
    @Since DATETIME2 (0)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(1) FROM [Main].[Verification] WHERE [Status] = 1 AND [Archived] = 0)                 AS [PendingVerifications],
        (SELECT COUNT(1) FROM [Safety].[Report] WHERE [Status] = 1 AND [Kind] <> 4 AND [Archived] = 0)     AS [OpenReports],
        (SELECT COUNT(1) FROM [Safety].[Report] WHERE [Status] = 1 AND [Kind] = 4 AND [Archived] = 0)      AS [OpenDisputes],
        (SELECT COUNT(1) FROM [Safety].[SosEvent] WHERE [Status] IN (1, 2) AND [Archived] = 0)             AS [OpenSos],
        (SELECT COUNT(1) FROM [Safety].[CheckIn] WHERE [Status] = 3 AND [DueAt] >= @Since AND [Archived] = 0) AS [MissedCheckIns],
        (SELECT COUNT(1) FROM [Pay].[Payout] WHERE [Status] = 1 AND [Archived] = 0)                        AS [PayoutsAwaitingApproval],
        (SELECT COUNT(1) FROM [Pay].[Refund] WHERE [Status] IN (1, 3) AND [Archived] = 0)                  AS [RefundsInFlight],
        (SELECT COUNT(1) FROM [Main].[Destination] WHERE [Status] = 3 AND [Archived] = 0)                  AS [ClosedDestinations],
        (SELECT COUNT(1) FROM [Main].[Destination] WHERE [Status] = 2 AND [Archived] = 0)                  AS [CautionDestinations],
        (SELECT COUNT(1) FROM [Main].[Trip] WHERE [Status] IN (2, 3) AND [Archived] = 0)                   AS [LiveTrips],
        (SELECT COUNT(1) FROM [Pay].[Booking] WHERE [Status] = 2 AND [ConfirmedOn] >= @Since)              AS [BookingsConfirmed];
END;

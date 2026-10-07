-- Marks every scheduled check-in more than @GraceMinutes past due as Missed, and returns them with
-- the trip and its host, for alerting the safety desk. Set-based and idempotent: a check-in is only
-- ever moved from Scheduled once.
CREATE PROCEDURE [Safety].[SetCheckInsMissed]
    @Now           DATETIME2 (0),
    @GraceMinutes  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Missed TABLE ([CheckInId] BIGINT NOT NULL PRIMARY KEY, [TripId] BIGINT NOT NULL, [Label] NVARCHAR (150) NOT NULL);

    UPDATE [Safety].[CheckIn]
    SET    [Status]    = 3,
           [UpdatedOn] = SYSUTCDATETIME()
    OUTPUT inserted.[Id], inserted.[TripId], inserted.[Label] INTO @Missed ([CheckInId], [TripId], [Label])
    WHERE  [Status] = 1
      AND  [Archived] = 0
      AND  [DueAt] < DATEADD(MINUTE, -@GraceMinutes, @Now);

    SELECT [m].[CheckInId], [m].[TripId], [m].[Label], [t].[HostId], [t].[Title] AS [TripTitle]
    FROM   @Missed       AS [m]
    JOIN   [Main].[Trip] AS [t] ON [t].[Id] = [m].[TripId];
END;

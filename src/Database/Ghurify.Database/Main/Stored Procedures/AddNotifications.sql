-- Stores a batch of notifications, skipping any a user already has with the same dedupe key, and
-- returns only the ones stored now (so only those are pushed live). Idempotent: raising the same
-- batch twice stores and pushes nothing the second time.
CREATE PROCEDURE [Main].[AddNotifications]
    @Items [Main].[NotificationList] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    INSERT INTO [Main].[Notification] ([UserId], [Kind], [Data], [DedupeKey])
    OUTPUT inserted.[Id], inserted.[UserId], inserted.[Kind], inserted.[Data], inserted.[Created]
    SELECT [i].[UserId], [i].[Kind], [i].[Data], [i].[DedupeKey]
    FROM   @Items AS [i]
    WHERE  NOT EXISTS (SELECT 1
                       FROM   [Main].[Notification] AS [n] WITH (UPDLOCK, HOLDLOCK)
                       WHERE  [n].[UserId] = [i].[UserId]
                         AND  [n].[DedupeKey] = [i].[DedupeKey]);

    COMMIT TRAN;
END;

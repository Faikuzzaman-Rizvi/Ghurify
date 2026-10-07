-- What an account may do: its status and gender, the roles it holds, and the strongest identity
-- check it has passed. Read on every authorized request, so it touches only narrow indexes.
--
-- Two result sets: the account (empty if it does not exist), then its roles.
CREATE PROCEDURE [Main].[GetUserAccess]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id]      AS [UserId],
           [u].[Status],
           [u].[Gender],
           (SELECT MAX([v].[Level])
            FROM   [Main].[Verification] AS [v]
            WHERE  [v].[UserId] = [u].[Id]
              AND  [v].[Status] = 2
              AND  [v].[Archived] = 0) AS [VerifiedLevel]
    FROM   [Main].[User] AS [u]
    WHERE  [u].[Id] = @UserId
      AND  [u].[Archived] = 0;

    SELECT [r].[Role]
    FROM   [Main].[UserRole] AS [r]
    WHERE  [r].[UserId] = @UserId
      AND  [r].[Archived] = 0;
END;

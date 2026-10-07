-- The account and its password hash for an email address, for checking a sign-in.
-- No row when there is no such account; the credential columns are NULL when the account has
-- never set a password (accounts created before passwords existed).
CREATE PROCEDURE [Main].[GetUserCredential]
    @Email NVARCHAR (256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id],
           [u].[Email],
           [u].[Phone],
           [u].[DisplayName],
           [u].[Gender],
           [u].[Status],
           [u].[Created],
           [c].[PasswordHash],
           [c].[PasswordSalt],
           [c].[Iterations],
           [c].[MustReset]
    FROM   [Main].[User] AS [u]
    LEFT JOIN [Main].[UserCredential] AS [c]
           ON [c].[UserId] = [u].[Id] AND [c].[Archived] = 0
    WHERE  [u].[Email] = @Email
      AND  [u].[Archived] = 0;
END;

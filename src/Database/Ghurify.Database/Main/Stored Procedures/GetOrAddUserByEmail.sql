-- Returns the account for an email address, creating it on first successful sign-in.
--
-- UPDLOCK/HOLDLOCK makes the "does it exist?" check and the insert one atomic step. Without it
-- two first-time logins racing on the same address would both insert and one would fail on
-- UX_User_Email, turning a normal sign-up into an error the user sees.
CREATE PROCEDURE [Main].[GetOrAddUserByEmail]
    @Email NVARCHAR (256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1
                   FROM   [Main].[User] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [Email] = @Email)
    BEGIN
        -- Status 1 = Active.
        INSERT INTO [Main].[User] ([Email], [Status])
        VALUES (@Email, 1);
    END;

    SELECT [Id],
           [Email],
           [Phone],
           [DisplayName],
           [Gender],
           [Status],
           [Created]
    FROM   [Main].[User]
    WHERE  [Email] = @Email;

    COMMIT TRAN;
END;

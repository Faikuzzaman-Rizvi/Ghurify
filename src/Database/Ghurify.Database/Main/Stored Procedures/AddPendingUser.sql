-- Registers a new account that is waiting for its owner to confirm the email address.
--
-- One atomic step, so two registrations racing on one address cannot both insert:
--   * no account yet        -> create it (Status 4 PendingEmail) with the password; @Result 0
--   * still pending         -> replace the name and password (the person may have mistyped
--                              and is trying again); @Result 1
--   * any other status      -> change nothing; @Result 2. The caller answers exactly as it
--                              does for the other two, so registration never reveals who has
--                              an account.
CREATE PROCEDURE [Main].[AddPendingUser]
    @Email         NVARCHAR (256),
    @DisplayName   NVARCHAR (100),
    @PasswordHash  VARBINARY (64),
    @PasswordSalt  VARBINARY (32),
    @Iterations    INT,
    @UserId        BIGINT  OUTPUT,
    @Result        TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UserId = NULL;
    SET @Result = 2;

    BEGIN TRAN;

    DECLARE @Status TINYINT;

    SELECT @UserId = [Id],
           @Status = [Status]
    FROM   [Main].[User] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Email] = @Email;

    IF @UserId IS NULL
    BEGIN
        INSERT INTO [Main].[User] ([Email], [DisplayName], [Status])
        VALUES (@Email, @DisplayName, 4);

        SET @UserId = SCOPE_IDENTITY();

        INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations], [UpdatedId])
        VALUES (@UserId, @PasswordHash, @PasswordSalt, @Iterations, @UserId);

        SET @Result = 0;
    END
    ELSE IF @Status = 4
    BEGIN
        UPDATE [Main].[User]
        SET    [DisplayName] = @DisplayName,
               [UpdatedOn]   = SYSUTCDATETIME(),
               [UpdatedId]   = @UserId
        WHERE  [Id] = @UserId;

        UPDATE [Main].[UserCredential]
        SET    [PasswordHash] = @PasswordHash,
               [PasswordSalt] = @PasswordSalt,
               [Iterations]   = @Iterations,
               [ChangedOn]    = SYSUTCDATETIME(),
               [UpdatedOn]    = SYSUTCDATETIME(),
               [UpdatedId]    = @UserId
        WHERE  [UserId] = @UserId;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations], [UpdatedId])
            VALUES (@UserId, @PasswordHash, @PasswordSalt, @Iterations, @UserId);
        END;

        SET @Result = 1;
    END;

    COMMIT TRAN;
END;

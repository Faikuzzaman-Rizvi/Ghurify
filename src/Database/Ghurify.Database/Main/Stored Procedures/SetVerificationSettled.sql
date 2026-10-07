-- Settles a pending identity check: from the provider's callback (found by @Provider and
-- @ProviderRef) or from the admin desk (found by @Id, with @ReviewedById).
--
-- Only a Pending check changes; a repeated callback or a second click finds it settled and
-- writes nothing. An approval re-checks "one national ID, one account" under lock.
--
-- @Result 0 = settled, 1 = not found, 2 = already settled, 3 = NID verifies another account
-- (for a callback, the check is rejected with that reason instead; for a review, nothing changes).
CREATE PROCEDURE [Main].[SetVerificationSettled]
    @Id            BIGINT        = NULL,
    @Provider      VARCHAR (30)  = NULL,
    @ProviderRef   VARCHAR (100) = NULL,
    @Status        TINYINT,
    @Reason        NVARCHAR (300),
    @ReviewedById  BIGINT        = NULL,
    @Result        TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @FoundId BIGINT, @UserId BIGINT, @CurrentStatus TINYINT, @NidHash VARBINARY (32);

    SELECT @FoundId       = [Id],
           @UserId        = [UserId],
           @CurrentStatus = [Status],
           @NidHash       = [NidHash]
    FROM   [Main].[Verification] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Archived] = 0
      AND  ((@Id IS NOT NULL AND [Id] = @Id)
            OR (@Id IS NULL AND [Provider] = @Provider AND [ProviderRef] = @ProviderRef));

    IF @FoundId IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @CurrentStatus <> 1
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status = 2
       AND @NidHash IS NOT NULL
       AND EXISTS (SELECT 1
                   FROM   [Main].[Verification] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [NidHash] = @NidHash
                     AND  [UserId] <> @UserId
                     AND  [Status] = 2
                     AND  [Archived] = 0)
    BEGIN
        SET @Result = 3;

        -- A provider approval cannot be refused back to the provider, so the check is closed as
        -- rejected. An admin is simply told, and decides again.
        IF @ReviewedById IS NULL
        BEGIN
            UPDATE [Main].[Verification]
            SET    [Status]     = 3,
                   [Reason]     = N'This national ID is already verified on another account.',
                   [ReviewedOn] = SYSUTCDATETIME(),
                   [UpdatedOn]  = SYSUTCDATETIME()
            WHERE  [Id] = @FoundId;
        END;

        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[Verification]
    SET    [Status]       = @Status,
           [Reason]       = @Reason,
           [ReviewedById] = @ReviewedById,
           [ReviewedOn]   = SYSUTCDATETIME(),
           [UpdatedOn]    = SYSUTCDATETIME(),
           [UpdatedId]    = @ReviewedById
    WHERE  [Id] = @FoundId;

    SET @Result = 0;

    COMMIT TRAN;
END;

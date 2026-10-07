-- Records an identity check. When the provider has already approved it, the "one national ID,
-- one account" rule is checked in the same transaction, so two accounts racing with the same
-- NID cannot both be approved.
--
-- The uploaded documents (@DocumentIds) are attached to the new check in the same transaction:
-- each must be the user's own, ready, and not already part of another check.
--
-- @Result 0 = saved, 3 = the ID already verifies another account, 4 = a document was not
-- usable. Nothing is written unless the result is 0.
CREATE PROCEDURE [Main].[AddVerification]
    @UserId       BIGINT,
    @Level        TINYINT,
    @Status       TINYINT,
    @NidHash      VARBINARY (32),
    @IdType       TINYINT,
    @DocumentIds  [Main].[IdList] READONLY,
    @Provider     VARCHAR (30),
    @ProviderRef  VARCHAR (100),
    @Reason       NVARCHAR (300),
    @Id           BIGINT  OUTPUT,
    @Result       TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @Result = 0;

    BEGIN TRAN;

    IF @Status = 2
       AND EXISTS (SELECT 1
                   FROM   [Main].[Verification] WITH (UPDLOCK, HOLDLOCK)
                   WHERE  [NidHash] = @NidHash
                     AND  [UserId] <> @UserId
                     AND  [Status] = 2
                     AND  [Archived] = 0)
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Main].[Verification]
           ([UserId], [Level], [Status], [NidHash], [IdType], [Provider], [ProviderRef], [Reason], [ReviewedOn], [UpdatedId])
    VALUES (@UserId, @Level, @Status, @NidHash, @IdType, @Provider, @ProviderRef, @Reason,
            CASE WHEN @Status = 1 THEN NULL ELSE SYSUTCDATETIME() END, @UserId);

    SET @Id = SCOPE_IDENTITY();

    UPDATE [d]
    SET    [VerificationId] = @Id,
           [UpdatedOn]      = SYSUTCDATETIME(),
           [UpdatedId]      = @UserId
    FROM   [Main].[VerificationDocument] AS [d]
    JOIN   @DocumentIds AS [ids] ON [ids].[Id] = [d].[Id]
    WHERE  [d].[UserId] = @UserId
      AND  [d].[Status] = 2
      AND  [d].[VerificationId] IS NULL
      AND  [d].[Archived] = 0;

    IF @@ROWCOUNT <> (SELECT COUNT(1) FROM @DocumentIds)
    BEGIN
        ROLLBACK TRAN;
        SET @Id = NULL;
        SET @Result = 4;
        RETURN;
    END;

    COMMIT TRAN;
END;

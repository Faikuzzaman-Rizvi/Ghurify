-- Sets (or, with a NULL blob, removes) a person's profile picture, creating their profile row if
-- they have never saved one. Returns the blob it replaced, so the caller can delete that file.
CREATE PROCEDURE [Main].[SetUserAvatar]
    @UserId      BIGINT,
    @AvatarBlob  NVARCHAR (200),
    @ActorId     BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Previous NVARCHAR (200);

    BEGIN TRAN;

    SELECT @Previous = [AvatarBlob]
    FROM   [Main].[UserProfile] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [UserId] = @UserId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Main].[UserProfile] ([UserId], [AvatarBlob], [AvatarUpdatedOn], [UpdatedId])
        VALUES (@UserId, @AvatarBlob, SYSUTCDATETIME(), @ActorId);
    END
    ELSE
    BEGIN
        UPDATE [Main].[UserProfile]
        SET    [AvatarBlob]      = @AvatarBlob,
               [AvatarUpdatedOn] = SYSUTCDATETIME(),
               [UpdatedOn]       = SYSUTCDATETIME(),
               [UpdatedId]       = @ActorId
        WHERE  [UserId] = @UserId;
    END;

    COMMIT TRAN;

    SELECT @Previous AS [PreviousBlob];
END;

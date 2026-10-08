-- Turns showing someone's travel map on their public profile on or off. The profile row is created
-- the first time it is needed (it is otherwise created when the profile is first saved).
CREATE PROCEDURE [Main].[SetTravelMapSharing]
    @UserId  BIGINT,
    @Share   BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE [Main].[UserProfile] WITH (UPDLOCK, SERIALIZABLE)
    SET    [ShareTravelMap] = @Share,
           [UpdatedOn]      = SYSUTCDATETIME(),
           [UpdatedId]      = @UserId
    WHERE  [UserId] = @UserId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Main].[UserProfile] ([UserId], [ShareTravelMap], [UpdatedId])
        VALUES (@UserId, @Share, @UserId);
    END;

    COMMIT TRAN;
END;

-- Replaces one of the site's images. The one it replaces is archived rather than overwritten, so
-- the previous logo can still be recovered from the database if a change turns out to be wrong.
--
-- @Result 0 = replaced an existing image, 1 = this kind had none before.
CREATE PROCEDURE [Site].[SetAsset]
    @Kind         VARCHAR (30),
    @ContentType  VARCHAR (40),
    @Bytes        VARBINARY (MAX),
    @ActorId      BIGINT,
    @Result       TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    UPDATE [Site].[Asset]
    SET    [Archived]  = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @ActorId
    WHERE  [Kind] = @Kind
      AND  [Archived] = 0;

    IF @@ROWCOUNT > 0
    BEGIN
        SET @Result = 0;
    END;

    INSERT INTO [Site].[Asset] ([Kind], [ContentType], [Bytes], [SizeBytes], [UploadedById], [UpdatedId])
    VALUES (@Kind, @ContentType, @Bytes, DATALENGTH(@Bytes), @ActorId, @ActorId);

    COMMIT TRAN;
END;

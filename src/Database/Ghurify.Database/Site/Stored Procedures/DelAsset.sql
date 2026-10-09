-- Puts one of the site's images back to the file the app shipped with, by archiving whatever was
-- uploaded. Nothing is deleted: the bytes stay, archived, so a mistaken removal is recoverable.
--
-- @Result 0 = removed, 1 = that kind was already on the built-in image.
CREATE PROCEDURE [Site].[DelAsset]
    @Kind    VARCHAR (30),
    @ActorId BIGINT,
    @Result  TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [Site].[Asset]
    SET    [Archived]  = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @ActorId
    WHERE  [Kind] = @Kind
      AND  [Archived] = 0;

    SET @Result = IIF(@@ROWCOUNT > 0, 0, 1);
END;

-- One uploaded image, to serve it. Returns nothing when that kind has never been replaced, and
-- the API then serves the file the app shipped with.
CREATE PROCEDURE [Site].[GetAsset]
    @Kind VARCHAR (30)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Kind], [ContentType], [Bytes], [SizeBytes], [UpdatedOn]
    FROM   [Site].[Asset]
    WHERE  [Kind] = @Kind
      AND  [Archived] = 0;
END;

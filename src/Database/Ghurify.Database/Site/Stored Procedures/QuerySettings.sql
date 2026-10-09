-- Every setting that has been changed from its default, plus the version stamp of each uploaded
-- image.
--
-- Read on the first request after any change and then served from memory, so it stays one round
-- trip: two small result sets over the whole of the site's configuration.
--
-- Result sets:
--   1. the settings that differ from their default (key, value);
--   2. the live images (kind, content type, size, when it was last replaced) — the bytes are
--      left out, because this is read to build the configuration, not to serve a picture.
CREATE PROCEDURE [Site].[QuerySettings]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [Key], [Value]
    FROM     [Site].[Setting]
    WHERE    [Archived] = 0
    ORDER BY [Key];

    SELECT   [Kind], [ContentType], [SizeBytes], [UpdatedOn]
    FROM     [Site].[Asset]
    WHERE    [Archived] = 0
    ORDER BY [Kind];
END;

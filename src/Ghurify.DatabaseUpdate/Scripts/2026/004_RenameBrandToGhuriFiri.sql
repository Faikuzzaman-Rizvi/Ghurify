-- The site is called GhuriFiri, not Ghurify.
--
-- The name lives in two places: the default in Ghurify.Domain.Site.SiteSettingsCatalog, which
-- the code change covers, and a row in [Site].[Setting] when a super admin has saved one. A
-- saved row wins over the default, so a database that was named in the panel would keep the
-- old spelling in the header and the footer while every other line on the site read the new
-- one. This brings the saved row along.
--
-- Only the earlier spellings are touched. A site that has deliberately been renamed to
-- something of its own keeps its name.
--
-- Idempotent: after it has run, no row matches.

UPDATE [Site].[Setting]
SET    [Value] = N'GhuriFiri',
       [UpdatedOn] = SYSUTCDATETIME()
WHERE  [Key] = 'site.name'
  AND  [Archived] = 0
  AND  [Value] COLLATE Latin1_General_CS_AS IN (N'Ghurify', N'Ghurifiri', N'GhuriFiry');

UPDATE [Site].[Setting]
SET    [Value] = N'ঘুরিফিরি',
       [UpdatedOn] = SYSUTCDATETIME()
WHERE  [Key] = 'site.name.bn'
  AND  [Archived] = 0
  AND  [Value] = N'ঘুরিফাই';

-- The description carries the name too, so the same two spellings move with it.
UPDATE [Site].[Setting]
SET    [Value] = REPLACE([Value] COLLATE Latin1_General_CS_AS, N'Ghurify', N'GhuriFiri'),
       [UpdatedOn] = SYSUTCDATETIME()
WHERE  [Key] = 'site.description'
  AND  [Archived] = 0
  AND  [Value] COLLATE Latin1_General_CS_AS LIKE N'%Ghurify%';

UPDATE [Site].[Setting]
SET    [Value] = REPLACE([Value], N'ঘুরিফাই', N'ঘুরিফিরি'),
       [UpdatedOn] = SYSUTCDATETIME()
WHERE  [Key] = 'site.description.bn'
  AND  [Archived] = 0
  AND  [Value] LIKE N'%ঘুরিফাই%';

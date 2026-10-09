-- Saves a batch of site settings, writes the history for each one that actually moved, and
-- returns what changed so the caller can audit it.
--
-- All of it in one transaction: a theme is a set of colours that have to be readable together,
-- so half a theme is worse than none of it.
--
-- An empty value means "back to the shipped default", which archives the row rather than storing
-- a copy of the default. A value equal to what is already stored writes nothing at all, so
-- saving a form without touching it leaves no history and no audit noise.
--
-- Returns one row per setting that moved: the key, what it was (NULL if it was on its default)
-- and what it is now (NULL if it is back on its default).
CREATE PROCEDURE [Site].[SetSettings]
    @Settings [Site].[SettingList] READONLY,
    @ActorId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Changes TABLE
    (
        [Key]      VARCHAR (60)   PRIMARY KEY,
        [OldValue] NVARCHAR (400) NULL,
        [NewValue] NVARCHAR (400) NULL
    );

    -- UPDLOCK/HOLDLOCK so two admins saving the same key cannot both read "unset" and both
    -- insert, which the unique index would then refuse.
    INSERT INTO @Changes ([Key], [OldValue], [NewValue])
    SELECT    [w].[Key],
              [s].[Value],
              NULLIF([w].[Value], N'')
    FROM      @Settings AS [w]
    LEFT JOIN [Site].[Setting] AS [s] WITH (UPDLOCK, HOLDLOCK)
                  ON [s].[Key] = [w].[Key] AND [s].[Archived] = 0
    -- Only the ones that move. NULL-safe, because either side may be absent.
    WHERE     ISNULL([s].[Value], N'~unset~') <> ISNULL(NULLIF([w].[Value], N''), N'~unset~');

    -- Back to the default: the row goes, so the catalogue's value is what the site reads.
    UPDATE [s]
    SET    [s].[Archived]  = 1,
           [s].[UpdatedOn] = SYSUTCDATETIME(),
           [s].[UpdatedId] = @ActorId
    FROM   [Site].[Setting] AS [s]
    JOIN   @Changes AS [c] ON [c].[Key] = [s].[Key]
    WHERE  [s].[Archived] = 0
      AND  [c].[NewValue] IS NULL;

    -- Changed from one explicit value to another.
    UPDATE [s]
    SET    [s].[Value]     = [c].[NewValue],
           [s].[UpdatedOn] = SYSUTCDATETIME(),
           [s].[UpdatedId] = @ActorId
    FROM   [Site].[Setting] AS [s]
    JOIN   @Changes AS [c] ON [c].[Key] = [s].[Key]
    WHERE  [s].[Archived] = 0
      AND  [c].[NewValue] IS NOT NULL;

    -- Set for the first time, or set again after a reset. A new row either way: the archived
    -- ones are history, and reviving one of several would be ambiguous.
    INSERT INTO [Site].[Setting] ([Key], [Value], [UpdatedId])
    SELECT [c].[Key], [c].[NewValue], @ActorId
    FROM   @Changes AS [c]
    WHERE  [c].[NewValue] IS NOT NULL
      AND  NOT EXISTS (SELECT 1
                       FROM  [Site].[Setting] AS [s]
                       WHERE [s].[Key] = [c].[Key] AND [s].[Archived] = 0);

    INSERT INTO [Site].[SettingHistory] ([Key], [OldValue], [NewValue], [ChangedById], [UpdatedId])
    SELECT [c].[Key], [c].[OldValue], [c].[NewValue], @ActorId, @ActorId
    FROM   @Changes AS [c];

    COMMIT TRAN;

    SELECT [Key], [OldValue], [NewValue] FROM @Changes ORDER BY [Key];
END;

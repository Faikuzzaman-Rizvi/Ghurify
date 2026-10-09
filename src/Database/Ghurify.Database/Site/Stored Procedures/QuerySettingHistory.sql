-- What one setting used to be, newest change first: the panel's "this was 'Ghurify' until
-- Tuesday, when Nusrat changed it".
--
-- Capped rather than paged: the question is always about the recent past, and a field nobody has
-- touched in a year has nothing worth scrolling through.
CREATE PROCEDURE [Site].[QuerySettingHistory]
    @Key  VARCHAR (60),
    @Take INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SET @Take = IIF(@Take < 1 OR @Take > 100, 20, @Take);

    SELECT   TOP (@Take)
             [h].[Id],
             [h].[Key],
             [h].[OldValue],
             [h].[NewValue],
             [h].[ChangedById],
             [u].[DisplayName] AS [ChangedByName],
             [h].[Created]
    FROM     [Site].[SettingHistory] AS [h]
    JOIN     [Main].[User]           AS [u] ON [u].[Id] = [h].[ChangedById]
    WHERE    [h].[Key] = @Key
      AND    [h].[Archived] = 0
    ORDER BY [h].[Id] DESC;
END;

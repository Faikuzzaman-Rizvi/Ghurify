-- Table-valued parameter for saving a batch of site settings in one transaction, so a theme
-- change is all-or-nothing rather than half-applied.
--
-- An empty [Value] means "back to the shipped default": the procedure deletes the row instead of
-- storing a copy of the default, which is what keeps "reset" meaningful.
CREATE TYPE [Site].[SettingList] AS TABLE
(
    [Key]   VARCHAR (60)   NOT NULL,
    [Value] NVARCHAR (400) NOT NULL,
    PRIMARY KEY CLUSTERED ([Key] ASC)
);

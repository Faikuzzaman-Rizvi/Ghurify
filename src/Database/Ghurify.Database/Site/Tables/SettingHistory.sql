-- Every change to a site setting, oldest to newest. Append-only: nothing updates or deletes
-- these rows.
--
-- Separate from [Safety].[AuditLog] on purpose. The audit log answers "what did this person do?"
-- and is keyed by actor and entity; this answers "what was the site called in March, and who
-- changed it?", which is a question about a key over time. Keeping it here also means the
-- panel's per-setting history survives any future pruning of the audit log.
--
-- OldValue is NULL when the setting had never been set (it was using its default), and NewValue
-- is NULL when it was reset back to the default.
CREATE TABLE [Site].[SettingHistory]
(
    [Id]          BIGINT          IDENTITY (1, 1) NOT NULL,
    [Key]         VARCHAR (60)    NOT NULL,
    [OldValue]    NVARCHAR (400)  NULL,
    [NewValue]    NVARCHAR (400)  NULL,
    [ChangedById] BIGINT          NOT NULL,

    [Archived]    BIT             CONSTRAINT [DF_SettingHistory_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)   CONSTRAINT [DF_SettingHistory_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)   CONSTRAINT [DF_SettingHistory_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT          NULL,

    CONSTRAINT [PK_SettingHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SettingHistory_User_ChangedById] FOREIGN KEY ([ChangedById]) REFERENCES [Main].[User] ([Id])
);
GO

-- The panel's "what did this used to be?" for one field.
CREATE NONCLUSTERED INDEX [IX_SettingHistory_Key]
    ON [Site].[SettingHistory] ([Key] ASC, [Created] DESC)
    INCLUDE ([OldValue], [NewValue], [ChangedById]);
GO

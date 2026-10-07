-- Who did what on the admin and safety desks: approving an ID check, closing a destination,
-- approving a payout. Append-only; nothing updates or deletes these rows.
--
-- Note holds the reason the actor typed. It must never carry personal data (no NID, no phone).
CREATE TABLE [Safety].[AuditLog]
(
    [Id]          BIGINT          IDENTITY (1, 1) NOT NULL,
    [ActorId]     BIGINT          NOT NULL,
    -- Dotted verb, e.g. verification.approve, destination.close, payout.approve.
    [Action]      VARCHAR (60)    NOT NULL,
    [EntityType]  VARCHAR (40)    NOT NULL,
    [EntityId]    BIGINT          NOT NULL,
    [Note]        NVARCHAR (500)  NULL,

    [Archived]    BIT             CONSTRAINT [DF_AuditLog_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)   CONSTRAINT [DF_AuditLog_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)   CONSTRAINT [DF_AuditLog_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT          NULL,

    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AuditLog_User_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Main].[User] ([Id])
);
GO

-- The history of one thing: every decision about this verification, payout or destination.
CREATE NONCLUSTERED INDEX [IX_AuditLog_Entity]
    ON [Safety].[AuditLog] ([EntityType] ASC, [EntityId] ASC, [Created] DESC);
GO

-- The admin dashboard's recent activity.
CREATE NONCLUSTERED INDEX [IX_AuditLog_Created]
    ON [Safety].[AuditLog] ([Created] DESC)
    INCLUDE ([ActorId], [Action], [EntityType], [EntityId]);
GO

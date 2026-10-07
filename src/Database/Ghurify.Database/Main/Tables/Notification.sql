-- Everything we tell a user in the app: a new join request, an approval with a payment deadline,
-- a refund, a closure alert. Pushed live over the notification hub and kept here so the bell
-- shows what was missed while offline.
--
-- Kind is a stable key ("join_request.new") the web app translates; Data carries the ids and
-- names the message needs, as JSON. DedupeKey makes sending idempotent: a job that runs twice,
-- or a webhook delivered twice, cannot notify twice.
CREATE TABLE [Main].[Notification]
(
    [Id]         BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]     BIGINT          NOT NULL,
    [Kind]       VARCHAR (60)    NOT NULL,
    [Data]       NVARCHAR (1000) NULL,
    [DedupeKey]  VARCHAR (120)   NOT NULL,
    [ReadOn]     DATETIME2 (0)   NULL,

    [Archived]   BIT             CONSTRAINT [DF_Notification_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)   CONSTRAINT [DF_Notification_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)   CONSTRAINT [DF_Notification_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT          NULL,

    CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Notification_Data] CHECK ([Data] IS NULL OR ISJSON([Data]) = 1)
);
GO

-- The same event is recorded once per person, however many times it is raised.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Notification_UserId_DedupeKey]
    ON [Main].[Notification] ([UserId] ASC, [DedupeKey] ASC);
GO

-- The bell: a user's newest first, and the unread count.
CREATE NONCLUSTERED INDEX [IX_Notification_UserId_Id]
    ON [Main].[Notification] ([UserId] ASC, [Id] DESC)
    INCLUDE ([ReadOn])
    WHERE [Archived] = 0;
GO

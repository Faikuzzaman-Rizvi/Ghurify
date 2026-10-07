-- Something a user reported: a person, a story, a trip, or a dispute over a booking.
--
-- Kind: 1 User, 2 Post, 3 Trip, 4 Dispute (TargetId is then a booking the reporter is part of).
-- Reason: 1 Harassment, 2 Fraud, 3 Unsafe, 4 Inappropriate, 5 Payment, 6 Other.
-- Status: 1 Open, 2 Actioned, 3 Dismissed. Resolution is what the moderator decided and why.
CREATE TABLE [Safety].[Report]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [ReporterId]    BIGINT          NOT NULL,
    [Kind]          TINYINT         NOT NULL,
    [TargetId]      BIGINT          NOT NULL,
    [Reason]        TINYINT         NOT NULL,
    [Details]       NVARCHAR (1000) NULL,
    [Status]        TINYINT         CONSTRAINT [DF_Report_Status] DEFAULT ((1)) NOT NULL,
    [Resolution]    NVARCHAR (500)  NULL,
    [ResolvedById]  BIGINT          NULL,
    [ResolvedOn]    DATETIME2 (0)   NULL,

    [Archived]      BIT             CONSTRAINT [DF_Report_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_Report_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_Report_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_Report] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Report_User_ReporterId] FOREIGN KEY ([ReporterId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Report_User_ResolvedById] FOREIGN KEY ([ResolvedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Report_Kind] CHECK ([Kind] BETWEEN 1 AND 4),
    CONSTRAINT [CK_Report_Reason] CHECK ([Reason] BETWEEN 1 AND 6),
    CONSTRAINT [CK_Report_Status] CHECK ([Status] BETWEEN 1 AND 3)
);
GO

-- The moderation queue: open reports of a kind, oldest first.
CREATE NONCLUSTERED INDEX [IX_Report_Kind_Status]
    ON [Safety].[Report] ([Kind] ASC, [Status] ASC, [Created] ASC)
    WHERE [Archived] = 0;
GO

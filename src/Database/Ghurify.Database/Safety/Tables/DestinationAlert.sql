-- Every change the safety desk makes to a destination's status, kept as history. The current status
-- itself lives on [Main].[Destination]; this table says who changed it, when, and why.
-- Status: 1 Open, 2 Caution, 3 Closed. ProcessedOn is set once a closure has cancelled the trips
-- and started the refunds, so a retried closure job does it once.
CREATE TABLE [Safety].[DestinationAlert]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [DestinationId]  BIGINT          NOT NULL,
    [Status]         TINYINT         NOT NULL,
    [Note]           NVARCHAR (300)  NULL,
    [NoteBn]         NVARCHAR (300)  NULL,
    [CreatedById]    BIGINT          NOT NULL,
    [ProcessedOn]    DATETIME2 (0)   NULL,

    [Archived]       BIT             CONSTRAINT [DF_DestinationAlert_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_DestinationAlert_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_DestinationAlert_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    CONSTRAINT [PK_DestinationAlert] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_DestinationAlert_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [Main].[Destination] ([Id]),
    CONSTRAINT [FK_DestinationAlert_User_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_DestinationAlert_Status] CHECK ([Status] >= 1 AND [Status] <= 3)
);
GO

CREATE NONCLUSTERED INDEX [IX_DestinationAlert_DestinationId]
    ON [Safety].[DestinationAlert] ([DestinationId] ASC, [Id] DESC);
GO

-- Police stations and hospitals near each destination, for the SOS screen's "nearest help".
-- Seeded by Script.PostDeployment.sql, insert-if-missing; the safety desk owns the rows after that.
-- Kind: 1 Police, 2 Hospital, 3 Tourist police. Phone is optional: the SOS screen always offers the
-- national emergency number 999 first. CheckedOn/CheckedById record that a person confirmed the
-- name, phone and position are right; seeded rows start unchecked.
CREATE TABLE [Safety].[EmergencyPoint]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [Code]           VARCHAR (60)    NOT NULL,
    [DestinationId]  BIGINT          NULL,
    [Kind]           TINYINT         NOT NULL,
    [Name]           NVARCHAR (150)  NOT NULL,
    [NameBn]         NVARCHAR (150)  NOT NULL,
    [Phone]          NVARCHAR (20)   NULL,
    [Location]       GEOGRAPHY       NOT NULL,
    [CheckedOn]      DATETIME2 (0)   NULL,
    [CheckedById]    BIGINT          NULL,

    [Archived]       BIT             CONSTRAINT [DF_EmergencyPoint_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_EmergencyPoint_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_EmergencyPoint_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    CONSTRAINT [PK_EmergencyPoint] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EmergencyPoint_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [Main].[Destination] ([Id]),
    CONSTRAINT [FK_EmergencyPoint_User_CheckedById] FOREIGN KEY ([CheckedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_EmergencyPoint_Kind] CHECK ([Kind] BETWEEN 1 AND 3)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_EmergencyPoint_Code]
    ON [Safety].[EmergencyPoint] ([Code] ASC);
GO

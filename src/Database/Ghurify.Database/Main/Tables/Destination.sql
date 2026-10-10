-- A place trips go to: Sajek, Bandarban, Cox's Bazar...
--
-- Lookup data, seeded insert-if-missing by Script.PostDeployment.sql. Status is the safety
-- signal: Caution shows a warning on every trip there, Closed blocks publishing new trips.
-- Names and summaries are curated by Ghurify, not by hosts, so they carry a Bangla copy.
CREATE TABLE [Main].[Destination]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    -- URL key, e.g. /destinations/sajek. ASCII only so it survives any link or QR code.
    [Slug]          VARCHAR (60)    NOT NULL,
    [Name]          NVARCHAR (100)  NOT NULL,
    [NameBn]        NVARCHAR (100)  NOT NULL,
    [Division]      NVARCHAR (50)   NOT NULL,
    [DivisionBn]    NVARCHAR (50)   NOT NULL,
    [Summary]       NVARCHAR (400)  NOT NULL,
    [SummaryBn]     NVARCHAR (400)  NOT NULL,
    -- 1 Hills, 2 Beach, 3 Island, 4 Forest, 5 Wetland, 6 TeaGarden, 7 Lake, 8 River.
    [Kind]          TINYINT         NOT NULL,
    [Location]      GEOGRAPHY       NULL,
    -- 1 Open, 2 Caution, 3 Closed.
    [Status]        TINYINT         CONSTRAINT [DF_Destination_Status] DEFAULT ((1)) NOT NULL,
    [StatusNote]    NVARCHAR (300)  NULL,
    [StatusNoteBn]  NVARCHAR (300)  NULL,

    [Archived]      BIT             CONSTRAINT [DF_Destination_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_Destination_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_Destination_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_Destination] PRIMARY KEY CLUSTERED ([Id] ASC),
    -- Written as NOT ... LIKE rather than NOT LIKE because that is how SQL Server stores it, and
    -- the dacpac compares the stored form: the other way round, every publish drops and recreates
    -- this constraint for no reason. Same for the other pattern checks in this project.
    CONSTRAINT [CK_Destination_Slug] CHECK (NOT [Slug] LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_CS_AS
                                            AND LEN([Slug]) > 0),
    CONSTRAINT [CK_Destination_Kind] CHECK ([Kind] >= 1 AND [Kind] <= 8),
    CONSTRAINT [CK_Destination_Status] CHECK ([Status] >= 1 AND [Status] <= 3)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Destination_Slug]
    ON [Main].[Destination] ([Slug] ASC);
GO

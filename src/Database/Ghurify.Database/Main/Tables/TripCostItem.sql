-- One line of a trip's per-person cost breakdown: transport, stay, food...
--
-- Showing travellers exactly where their money goes is the product's pricing promise, so the
-- lines must add up to [Main].[Trip].[PricePerPerson].
CREATE TABLE [Main].[TripCostItem]
(
    [Id]           BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]       BIGINT          NOT NULL,
    -- 1 Transport, 2 Stay, 3 Food, 4 Fees, 5 Guide, 6 Buffer.
    [Category]     TINYINT         NOT NULL,
    [Description]  NVARCHAR (150)  NULL,
    [Amount]       DECIMAL (18, 2) NOT NULL,
    [SortOrder]    TINYINT         CONSTRAINT [DF_TripCostItem_SortOrder] DEFAULT ((0)) NOT NULL,

    [Archived]     BIT             CONSTRAINT [DF_TripCostItem_Archived] DEFAULT ((0)) NOT NULL,
    [Created]      DATETIME2 (0)   CONSTRAINT [DF_TripCostItem_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]    DATETIME2 (7)   CONSTRAINT [DF_TripCostItem_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]    BIGINT          NULL,

    CONSTRAINT [PK_TripCostItem] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_TripCostItem_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [CK_TripCostItem_Category] CHECK ([Category] >= 1 AND [Category] <= 6),
    CONSTRAINT [CK_TripCostItem_Amount] CHECK ([Amount] >= 0)
);
GO

CREATE NONCLUSTERED INDEX [IX_TripCostItem_TripId]
    ON [Main].[TripCostItem] ([TripId] ASC, [SortOrder] ASC)
    INCLUDE ([Category], [Description], [Amount]);
GO

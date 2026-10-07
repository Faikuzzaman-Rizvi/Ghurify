-- A trip's cost breakdown, passed to AddTrip and SetTrip in one parameter so the trip and its
-- lines are written in a single call and a single transaction.
CREATE TYPE [Main].[TripCostItemList] AS TABLE
(
    [SortOrder]    TINYINT          NOT NULL,
    [Category]     TINYINT          NOT NULL,
    [Description]  NVARCHAR (150)   NULL,
    [Amount]       DECIMAL (18, 2)  NOT NULL,
    PRIMARY KEY CLUSTERED ([SortOrder] ASC)
);

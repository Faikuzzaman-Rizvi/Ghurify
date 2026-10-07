-- A trip's day-by-day plan, passed to AddTrip and SetTrip alongside the cost breakdown.
CREATE TYPE [Main].[ItineraryDayList] AS TABLE
(
    [DayNo]       TINYINT          NOT NULL,
    [Title]       NVARCHAR (150)   NOT NULL,
    [Details]     NVARCHAR (1000)  NOT NULL,
    [Difficulty]  TINYINT          NOT NULL,
    PRIMARY KEY CLUSTERED ([DayNo] ASC)
);

-- One day of a trip's plan: where the group goes and how hard the day is.
CREATE TABLE [Main].[ItineraryDay]
(
    [Id]          BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]      BIGINT          NOT NULL,
    [DayNo]       TINYINT         NOT NULL,
    [Title]       NVARCHAR (150)  NOT NULL,
    [Details]     NVARCHAR (1000) NOT NULL,
    -- 1 Easy, 2 Moderate, 3 Challenging.
    [Difficulty]  TINYINT         CONSTRAINT [DF_ItineraryDay_Difficulty] DEFAULT ((1)) NOT NULL,

    [Archived]    BIT             CONSTRAINT [DF_ItineraryDay_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)   CONSTRAINT [DF_ItineraryDay_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)   CONSTRAINT [DF_ItineraryDay_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT          NULL,

    CONSTRAINT [PK_ItineraryDay] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ItineraryDay_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [CK_ItineraryDay_DayNo] CHECK ([DayNo] >= 1),
    CONSTRAINT [CK_ItineraryDay_Difficulty] CHECK ([Difficulty] BETWEEN 1 AND 3)
);
GO

-- One plan per day per trip; also the lookup order for the trip page.
CREATE UNIQUE NONCLUSTERED INDEX [UX_ItineraryDay_TripId_DayNo]
    ON [Main].[ItineraryDay] ([TripId] ASC, [DayNo] ASC)
    WHERE [Archived] = 0;
GO

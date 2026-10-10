-- A review after a completed trip, between two people who were on it.
--
-- Direction: 1 Traveller reviews the host, 2 Host reviews a traveller, 3 Traveller reviews a guide.
-- One review per reviewer, reviewee and direction per trip: the unique index enforces it.
-- Only people who actually travelled (the host, travellers with a paid seat) may review, and only
-- after the trip is completed; AddReview checks both under lock.
CREATE TABLE [Social].[Review]
(
    [Id]          BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]      BIGINT          NOT NULL,
    [ReviewerId]  BIGINT          NOT NULL,
    [RevieweeId]  BIGINT          NOT NULL,
    [Direction]   TINYINT         NOT NULL,
    [Rating]      TINYINT         NOT NULL,
    [Body]        NVARCHAR (1000) NULL,

    [Archived]    BIT             CONSTRAINT [DF_Review_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)   CONSTRAINT [DF_Review_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)   CONSTRAINT [DF_Review_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT          NULL,

    CONSTRAINT [PK_Review] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Review_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_Review_User_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Review_User_RevieweeId] FOREIGN KEY ([RevieweeId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Review_Direction] CHECK ([Direction] >= 1 AND [Direction] <= 3),
    CONSTRAINT [CK_Review_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
    CONSTRAINT [CK_Review_NotSelf] CHECK ([ReviewerId] <> [RevieweeId])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Review_Trip_Reviewer_Reviewee_Direction]
    ON [Social].[Review] ([TripId] ASC, [ReviewerId] ASC, [RevieweeId] ASC, [Direction] ASC);
GO

-- Reviews a person received (profile page).
CREATE NONCLUSTERED INDEX [IX_Review_RevieweeId]
    ON [Social].[Review] ([RevieweeId] ASC, [Id] DESC)
    INCLUDE ([Direction], [Rating])
    WHERE [Archived] = 0;
GO

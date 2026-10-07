-- A person's ratings in one row, kept up to date by AddReview, so trip cards and profiles show a
-- host's average without averaging every review on every page view.
CREATE TABLE [Social].[RatingSummary]
(
    [Id]               BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]           BIGINT         NOT NULL,
    [AsHostCount]      INT            CONSTRAINT [DF_RatingSummary_AsHostCount] DEFAULT ((0)) NOT NULL,
    [AsHostAverage]    DECIMAL (3, 2) NULL,
    [AsTravelerCount]  INT            CONSTRAINT [DF_RatingSummary_AsTravelerCount] DEFAULT ((0)) NOT NULL,
    [AsTravelerAverage] DECIMAL (3, 2) NULL,

    [Archived]         BIT            CONSTRAINT [DF_RatingSummary_Archived] DEFAULT ((0)) NOT NULL,
    [Created]          DATETIME2 (0)  CONSTRAINT [DF_RatingSummary_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]        DATETIME2 (7)  CONSTRAINT [DF_RatingSummary_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]        BIGINT         NULL,

    CONSTRAINT [PK_RatingSummary] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RatingSummary_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_RatingSummary_UserId]
    ON [Social].[RatingSummary] ([UserId] ASC);
GO

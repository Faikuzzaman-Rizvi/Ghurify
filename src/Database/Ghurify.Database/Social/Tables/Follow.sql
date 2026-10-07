-- One user following another, for the feed. Unfollowing archives the row.
CREATE TABLE [Social].[Follow]
(
    [Id]          BIGINT         IDENTITY (1, 1) NOT NULL,
    [FollowerId]  BIGINT         NOT NULL,
    [FolloweeId]  BIGINT         NOT NULL,

    [Archived]    BIT            CONSTRAINT [DF_Follow_Archived] DEFAULT ((0)) NOT NULL,
    [Created]     DATETIME2 (0)  CONSTRAINT [DF_Follow_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]   DATETIME2 (7)  CONSTRAINT [DF_Follow_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]   BIGINT         NULL,

    CONSTRAINT [PK_Follow] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Follow_User_FollowerId] FOREIGN KEY ([FollowerId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Follow_User_FolloweeId] FOREIGN KEY ([FolloweeId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Follow_NotSelf] CHECK ([FollowerId] <> [FolloweeId])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Follow_FollowerId_FolloweeId]
    ON [Social].[Follow] ([FollowerId] ASC, [FolloweeId] ASC)
    WHERE [Archived] = 0;
GO

-- Follower counts on a profile.
CREATE NONCLUSTERED INDEX [IX_Follow_FolloweeId]
    ON [Social].[Follow] ([FolloweeId] ASC)
    WHERE [Archived] = 0;
GO

-- A user liking a post. One live like per user per post; unliking archives the row.
CREATE TABLE [Social].[Like]
(
    [Id]         BIGINT         IDENTITY (1, 1) NOT NULL,
    [PostId]     BIGINT         NOT NULL,
    [UserId]     BIGINT         NOT NULL,

    [Archived]   BIT            CONSTRAINT [DF_Like_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)  CONSTRAINT [DF_Like_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)  CONSTRAINT [DF_Like_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT         NULL,

    CONSTRAINT [PK_Like] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Like_Post] FOREIGN KEY ([PostId]) REFERENCES [Social].[Post] ([Id]),
    CONSTRAINT [FK_Like_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Like_PostId_UserId]
    ON [Social].[Like] ([PostId] ASC, [UserId] ASC)
    WHERE [Archived] = 0;
GO

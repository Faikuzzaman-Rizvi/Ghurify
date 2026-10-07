-- A comment on a post. Deleting is archiving.
CREATE TABLE [Social].[Comment]
(
    [Id]         BIGINT          IDENTITY (1, 1) NOT NULL,
    [PostId]     BIGINT          NOT NULL,
    [AuthorId]   BIGINT          NOT NULL,
    [Body]       NVARCHAR (1000) NOT NULL,

    [Archived]   BIT             CONSTRAINT [DF_Comment_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)   CONSTRAINT [DF_Comment_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)   CONSTRAINT [DF_Comment_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT          NULL,

    CONSTRAINT [PK_Comment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Comment_Post] FOREIGN KEY ([PostId]) REFERENCES [Social].[Post] ([Id]),
    CONSTRAINT [FK_Comment_User_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Main].[User] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_Comment_PostId_Id]
    ON [Social].[Comment] ([PostId] ASC, [Id] ASC)
    WHERE [Archived] = 0;
GO

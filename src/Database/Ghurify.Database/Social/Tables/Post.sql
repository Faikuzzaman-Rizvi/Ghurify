-- A story: text, optionally photos or videos (Social.Media), optionally about a destination or a
-- trip. Status: 1 Published, 2 Hidden (by a moderator, after a report). Deleting is archiving.
CREATE TABLE [Social].[Post]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [AuthorId]       BIGINT          NOT NULL,
    [DestinationId]  BIGINT          NULL,
    [TripId]         BIGINT          NULL,
    [Body]           NVARCHAR (2000) NOT NULL,
    [Status]         TINYINT         CONSTRAINT [DF_Post_Status] DEFAULT ((1)) NOT NULL,

    [Archived]       BIT             CONSTRAINT [DF_Post_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_Post_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_Post_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,
    -- When the author last changed the text, place or photos (shown as 'edited'); NULL if never.
    -- Last, so adding it was a plain ADD COLUMN rather than a rebuild of the table.
    [EditedOn]       DATETIME2 (0)   NULL,

    CONSTRAINT [PK_Post] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Post_User_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Post_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [Main].[Destination] ([Id]),
    CONSTRAINT [FK_Post_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [CK_Post_Status] CHECK ([Status] >= 1 AND [Status] <= 2)
);
GO

-- An author's posts (profile page, and the feed for their followers).
CREATE NONCLUSTERED INDEX [IX_Post_AuthorId_Id]
    ON [Social].[Post] ([AuthorId] ASC, [Id] DESC)
    WHERE [Archived] = 0 AND [Status] = 1;
GO

-- Destination stories in the feed and on destination pages.
CREATE NONCLUSTERED INDEX [IX_Post_DestinationId_Id]
    ON [Social].[Post] ([DestinationId] ASC, [Id] DESC)
    WHERE [Archived] = 0 AND [Status] = 1 AND [DestinationId] IS NOT NULL;
GO

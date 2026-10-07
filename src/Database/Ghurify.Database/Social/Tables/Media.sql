-- A photo or video a user uploads straight to Blob storage with a short-lived write link.
--
-- Kind: 1 Image, 2 Video. Status: 1 AwaitingUpload (link issued), 2 Processing (uploaded; the
-- processing job is checking the file and stripping location metadata), 3 Ready (safe to show),
-- 4 Failed (rejected; Failure says why). Only Ready media is ever shown to anyone else.
-- UploadBlob is where the user's raw file lands; ProcessedBlob is the cleaned copy that is served.
CREATE TABLE [Social].[Media]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [OwnerId]        BIGINT          NOT NULL,
    [PostId]         BIGINT          NULL,
    [Kind]           TINYINT         NOT NULL,
    [ContentType]    VARCHAR (100)   NOT NULL,
    [UploadBlob]     VARCHAR (200)   NOT NULL,
    [ProcessedBlob]  VARCHAR (200)   NULL,
    [SizeBytes]      BIGINT          NULL,
    [Status]         TINYINT         CONSTRAINT [DF_Media_Status] DEFAULT ((1)) NOT NULL,
    [Failure]        NVARCHAR (200)  NULL,

    [Archived]       BIT             CONSTRAINT [DF_Media_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_Media_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_Media_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    CONSTRAINT [PK_Media] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Media_User_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Media_Post] FOREIGN KEY ([PostId]) REFERENCES [Social].[Post] ([Id]),
    CONSTRAINT [CK_Media_Kind] CHECK ([Kind] BETWEEN 1 AND 2),
    CONSTRAINT [CK_Media_Status] CHECK ([Status] BETWEEN 1 AND 4)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Media_UploadBlob]
    ON [Social].[Media] ([UploadBlob] ASC);
GO

-- A post's media, in upload order.
CREATE NONCLUSTERED INDEX [IX_Media_PostId]
    ON [Social].[Media] ([PostId] ASC, [Id] ASC)
    INCLUDE ([Kind], [Status], [ProcessedBlob])
    WHERE [PostId] IS NOT NULL;
GO

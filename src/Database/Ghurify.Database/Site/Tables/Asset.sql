-- The logo, the favicon and the link-preview picture, as uploaded.
--
-- The bytes are here rather than in blob storage, which is where every other image on the
-- platform lives. Three reasons, and they are specific to these five images:
--   * they are tiny and few (a logo, two icons, a dark variant, one social picture);
--   * they must be readable by any browser at a stable address, with no expiring link, which is
--     the opposite of what the private SAS-based media container is built for;
--   * the site has to be able to render its own name and mark even if the storage account is
--     unreachable, and a header without its logo is a visibly broken site.
-- A sixth, larger image would belong in blob storage instead.
--
-- The API serves these from [Site].[GetAsset] with a long cache lifetime and an ETag built from
-- UpdatedOn, so a replaced logo appears at once and an unchanged one is never re-sent.
CREATE TABLE [Site].[Asset]
(
    [Id]           BIGINT          IDENTITY (1, 1) NOT NULL,
    -- logo, logo-dark, favicon, apple-touch-icon, social-image.
    [Kind]         VARCHAR (30)    NOT NULL,
    [ContentType]  VARCHAR (40)    NOT NULL,
    -- Already checked to be a real image and stripped of its metadata before it got here.
    [Bytes]        VARBINARY (MAX) NOT NULL,
    [SizeBytes]    INT             NOT NULL,
    -- Who uploaded it, so the audit trail and the panel can say.
    [UploadedById] BIGINT          NOT NULL,

    [Archived]     BIT             CONSTRAINT [DF_Asset_Archived] DEFAULT ((0)) NOT NULL,
    [Created]      DATETIME2 (0)   CONSTRAINT [DF_Asset_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]    DATETIME2 (7)   CONSTRAINT [DF_Asset_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]    BIGINT          NULL,

    CONSTRAINT [PK_Asset] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Asset_User_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Asset_Kind] CHECK ([Kind] NOT LIKE '%[^a-z-]%' COLLATE Latin1_General_CS_AS
                                      AND LEN([Kind]) > 0),
    CONSTRAINT [CK_Asset_SizeBytes] CHECK ([SizeBytes] > 0)
);
GO

-- One live image per kind. Archived rows are what it used to be.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Asset_Kind]
    ON [Site].[Asset] ([Kind] ASC)
    WHERE [Archived] = 0;
GO

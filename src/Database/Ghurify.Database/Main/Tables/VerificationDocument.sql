-- Photos a person uploads to prove who they are: their ID document (both sides of an NID or
-- driving licence, or a passport photo page), a selfie holding it, and optionally a guide or
-- operator licence.
--
-- The images live in a private storage container of their own, never with story media, and are
-- only ever shown to staff through short-lived links (every viewing is audited). They are
-- deleted 30 days after the check they belong to is decided; uploads never submitted are deleted
-- after 7 days. The row stays, with PurgedOn set, as the record that a document was seen.
--
-- Kind: 1 NID front, 2 NID back, 3 Passport photo page, 4 Driving licence front,
--       5 Driving licence back, 6 Selfie holding the ID, 7 Guide or operator licence.
-- Status: 1 Awaiting upload, 2 Ready (checked, metadata removed), 3 Rejected (not a valid image),
--         4 Purged (images deleted).
CREATE TABLE [Main].[VerificationDocument]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]          BIGINT          NOT NULL,
    -- Set when the document is submitted with a check; null while it is only uploaded.
    [VerificationId]  BIGINT          NULL,
    [Kind]            TINYINT         NOT NULL,
    [Status]          TINYINT         CONSTRAINT [DF_VerificationDocument_Status] DEFAULT ((1)) NOT NULL,
    [ContentType]     VARCHAR (50)    NOT NULL,
    [UploadBlob]      NVARCHAR (200)  NOT NULL,
    [Blob]            NVARCHAR (200)  NULL,
    [SizeBytes]       BIGINT          NULL,
    -- SHA-256 of the stored image, so a re-used photo can be spotted without keeping the photo.
    [Sha256]          VARBINARY (32)  NULL,
    [FailureReason]   NVARCHAR (200)  NULL,
    [PurgedOn]        DATETIME2 (0)   NULL,

    [Archived]        BIT             CONSTRAINT [DF_VerificationDocument_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_VerificationDocument_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_VerificationDocument_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    CONSTRAINT [PK_VerificationDocument] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_VerificationDocument_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_VerificationDocument_Verification] FOREIGN KEY ([VerificationId]) REFERENCES [Main].[Verification] ([Id]),
    CONSTRAINT [CK_VerificationDocument_Kind] CHECK ([Kind] >= 1 AND [Kind] <= 7),
    CONSTRAINT [CK_VerificationDocument_Status] CHECK ([Status] >= 1 AND [Status] <= 4)
);
GO

-- A person's own uploads, and what is waiting to be submitted.
CREATE NONCLUSTERED INDEX [IX_VerificationDocument_UserId_Status]
    ON [Main].[VerificationDocument] ([UserId] ASC, [Status] ASC)
    INCLUDE ([VerificationId], [Kind]);
GO

-- The documents of one check, for the reviewer.
CREATE NONCLUSTERED INDEX [IX_VerificationDocument_VerificationId]
    ON [Main].[VerificationDocument] ([VerificationId] ASC)
    WHERE [VerificationId] IS NOT NULL;
GO

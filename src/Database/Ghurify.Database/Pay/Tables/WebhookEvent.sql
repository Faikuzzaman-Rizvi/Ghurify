-- Every callback a payment gateway sent us, kept as received.
--
-- (Provider, EventId) is unique, so a callback delivered twice is recognised and processed once.
-- Callbacks with a bad signature are stored too (SignatureValid = 0), for investigation, and are
-- never processed. ProcessedOn is set once the payment it reports has been settled.
CREATE TABLE [Pay].[WebhookEvent]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [Provider]        VARCHAR (20)    NOT NULL,
    [EventId]         VARCHAR (150)   NOT NULL,
    [TransactionRef]  VARCHAR (40)    NULL,
    [Payload]         NVARCHAR (4000) NOT NULL,
    [SignatureValid]  BIT             NOT NULL,
    [ProcessedOn]     DATETIME2 (0)   NULL,
    [Outcome]         VARCHAR (40)    NULL,

    [Archived]        BIT             CONSTRAINT [DF_WebhookEvent_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_WebhookEvent_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_WebhookEvent_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    CONSTRAINT [PK_WebhookEvent] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_WebhookEvent_Provider_EventId]
    ON [Pay].[WebhookEvent] ([Provider] ASC, [EventId] ASC);
GO

-- Every callback for one payment, for the admin desk's payment detail.
CREATE NONCLUSTERED INDEX [IX_WebhookEvent_TransactionRef]
    ON [Pay].[WebhookEvent] ([TransactionRef] ASC);
GO

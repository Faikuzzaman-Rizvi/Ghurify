-- Money going back to a traveller, and where the gateway refund stands.
--
-- Reason: 1 Traveller cancelled, 2 Host cancelled, 3 Destination closed, 4 Paid after the seat
-- was gone, 5 Paid twice, 6 Paid amount did not match, 7 Admin decision.
-- Status: 1 Pending (the ledger Refund entry is written; the gateway call is in flight or will be
-- retried), 2 Succeeded, 3 Failed (retried by the refund job).
-- Shortfall is what the rules said was owed but escrow no longer held (for example after a first
-- payout): it is recorded for the admin desk to recover, never paid out of thin air.
-- Reference is unique, so asking for the same refund twice creates it once.
CREATE TABLE [Pay].[Refund]
(
    [Id]                 BIGINT          IDENTITY (1, 1) NOT NULL,
    [BookingId]          BIGINT          NOT NULL,
    [PaymentId]          BIGINT          NOT NULL,
    [Amount]             DECIMAL (18, 2) NOT NULL,
    [Shortfall]          DECIMAL (18, 2) CONSTRAINT [DF_Refund_Shortfall] DEFAULT ((0)) NOT NULL,
    [Reason]             TINYINT         NOT NULL,
    [Status]             TINYINT         CONSTRAINT [DF_Refund_Status] DEFAULT ((1)) NOT NULL,
    [Reference]          VARCHAR (100)   NOT NULL,
    [ProviderRefundRef]  VARCHAR (100)   NULL,
    [FailureReason]      NVARCHAR (300)  NULL,
    [Attempts]           TINYINT         CONSTRAINT [DF_Refund_Attempts] DEFAULT ((0)) NOT NULL,
    [CompletedOn]        DATETIME2 (0)   NULL,

    [Archived]           BIT             CONSTRAINT [DF_Refund_Archived] DEFAULT ((0)) NOT NULL,
    [Created]            DATETIME2 (0)   CONSTRAINT [DF_Refund_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]          DATETIME2 (7)   CONSTRAINT [DF_Refund_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]          BIGINT          NULL,

    CONSTRAINT [PK_Refund] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Refund_Booking] FOREIGN KEY ([BookingId]) REFERENCES [Pay].[Booking] ([Id]),
    CONSTRAINT [FK_Refund_Payment] FOREIGN KEY ([PaymentId]) REFERENCES [Pay].[Payment] ([Id]),
    CONSTRAINT [CK_Refund_Amount] CHECK ([Amount] > 0 AND [Shortfall] >= 0),
    CONSTRAINT [CK_Refund_Reason] CHECK ([Reason] BETWEEN 1 AND 7),
    CONSTRAINT [CK_Refund_Status] CHECK ([Status] BETWEEN 1 AND 3)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Refund_Reference]
    ON [Pay].[Refund] ([Reference] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Refund_BookingId]
    ON [Pay].[Refund] ([BookingId] ASC)
    INCLUDE ([Status], [Amount]);
GO

-- The retry job: refunds still waiting on the gateway.
CREATE NONCLUSTERED INDEX [IX_Refund_Status]
    ON [Pay].[Refund] ([Status] ASC)
    INCLUDE ([Attempts])
    WHERE [Status] IN (1, 3);
GO

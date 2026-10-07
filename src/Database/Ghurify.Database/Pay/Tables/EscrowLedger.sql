-- The escrow ledger: every movement of a booking's money, as an append-only list of entries.
--
-- EntryType: 1 Hold (the traveller's payment enters escrow), 2 Release (paid out of escrow to the
-- host, or the service fee to the platform), 3 Refund (back to the traveller).
-- Counterparty: 1 Traveller, 2 Host, 3 Platform.
-- For every booking, the money still in escrow is Hold - Release - Refund, and it can never go
-- below zero: the procedures that write releases and refunds check it under lock.
--
-- APPEND-ONLY. There are no UPDATE or DELETE procedures for this table, and there must never be.
-- A mistake is corrected with a new, opposite entry. Reference is unique, so retrying the step that
-- wrote an entry can never write it twice.
CREATE TABLE [Pay].[EscrowLedger]
(
    [Id]            BIGINT          IDENTITY (1, 1) NOT NULL,
    [BookingId]     BIGINT          NOT NULL,
    [PaymentId]     BIGINT          NULL,
    [EntryType]     TINYINT         NOT NULL,
    [Amount]        DECIMAL (18, 2) NOT NULL,
    [Counterparty]  TINYINT         NOT NULL,
    [Reference]     VARCHAR (100)   NOT NULL,

    [Archived]      BIT             CONSTRAINT [DF_EscrowLedger_Archived] DEFAULT ((0)) NOT NULL,
    [Created]       DATETIME2 (0)   CONSTRAINT [DF_EscrowLedger_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]     DATETIME2 (7)   CONSTRAINT [DF_EscrowLedger_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]     BIGINT          NULL,

    CONSTRAINT [PK_EscrowLedger] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EscrowLedger_Booking] FOREIGN KEY ([BookingId]) REFERENCES [Pay].[Booking] ([Id]),
    CONSTRAINT [FK_EscrowLedger_Payment] FOREIGN KEY ([PaymentId]) REFERENCES [Pay].[Payment] ([Id]),
    CONSTRAINT [CK_EscrowLedger_EntryType] CHECK ([EntryType] BETWEEN 1 AND 3),
    CONSTRAINT [CK_EscrowLedger_Counterparty] CHECK ([Counterparty] BETWEEN 1 AND 3),
    CONSTRAINT [CK_EscrowLedger_Amount] CHECK ([Amount] > 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_EscrowLedger_Reference]
    ON [Pay].[EscrowLedger] ([Reference] ASC);
GO

-- A booking's balance, summed on every release and refund.
CREATE NONCLUSTERED INDEX [IX_EscrowLedger_BookingId]
    ON [Pay].[EscrowLedger] ([BookingId] ASC)
    INCLUDE ([EntryType], [Amount], [Counterparty]);
GO

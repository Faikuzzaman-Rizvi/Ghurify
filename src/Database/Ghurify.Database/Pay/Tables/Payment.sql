-- One attempt to pay for a booking through a payment gateway.
--
-- Amount (the booking's price), Fee (the service fee shown at checkout) and Total (what the
-- gateway is asked to charge) all come from the server; the browser never sends an amount.
-- Status: 1 Created (before the gateway was called), 2 Pending (the traveller was sent to the
-- gateway), 3 Succeeded (money received, verified server-to-server), 4 Failed, 5 Expired (the
-- seat hold ran out while it was pending).
-- IdempotencyKey is the client's key for this attempt: retrying with it returns this row instead
-- of charging again. TransactionRef is our id for the attempt at the gateway; ProviderTxnId is the
-- gateway's own transaction id, unique so one real payment can never be counted twice.
CREATE TABLE [Pay].[Payment]
(
    [Id]                 BIGINT          IDENTITY (1, 1) NOT NULL,
    [BookingId]          BIGINT          NOT NULL,
    [UserId]             BIGINT          NOT NULL,
    [Provider]           VARCHAR (20)    NOT NULL,
    [IdempotencyKey]     VARCHAR (64)    NOT NULL,
    [TransactionRef]     VARCHAR (40)    NOT NULL,
    [Amount]             DECIMAL (18, 2) NOT NULL,
    [Fee]                DECIMAL (18, 2) NOT NULL,
    [Total]              DECIMAL (18, 2) NOT NULL,
    [Currency]           CHAR (3)        CONSTRAINT [DF_Payment_Currency] DEFAULT ('BDT') NOT NULL,
    [Status]             TINYINT         CONSTRAINT [DF_Payment_Status] DEFAULT ((1)) NOT NULL,
    [ProviderSessionId]  VARCHAR (100)   NULL,
    [ProviderTxnId]      VARCHAR (100)   NULL,
    [RedirectUrl]        NVARCHAR (500)  NULL,
    -- What was actually charged, as reported by the gateway's server-side validation.
    [PaidAmount]         DECIMAL (18, 2) NULL,
    [FailureReason]      NVARCHAR (300)  NULL,
    [CompletedOn]        DATETIME2 (0)   NULL,

    -- How the traveller paid, from the gateway's server-side validation (never from the browser).
    -- MethodType: 1 Card, 2 Mobile banking (bKash, Nagad, Rocket...), 3 Internet banking, 4 Other.
    -- MethodName is the wallet or card network ("bKash", "Visa"); Issuer the bank or provider.
    -- AccountLast4 is all we keep of the card or wallet number: never a full card number.
    -- ValidationId is the gateway's id for the validation that confirmed the payment, and
    -- GatewayPaidOn when the gateway says the money moved (UTC). StoreAmount is what the gateway
    -- settles to us after its own charge; RiskFlagged is the gateway's fraud flag.
    [MethodType]         TINYINT         NULL,
    [MethodName]         NVARCHAR (60)   NULL,
    [AccountLast4]       VARCHAR (4)     NULL,
    [Issuer]             NVARCHAR (100)  NULL,
    [ValidationId]       VARCHAR (100)   NULL,
    [GatewayPaidOn]      DATETIME2 (0)   NULL,
    [StoreAmount]        DECIMAL (18, 2) NULL,
    [RiskFlagged]        BIT             NULL,

    [Archived]           BIT             CONSTRAINT [DF_Payment_Archived] DEFAULT ((0)) NOT NULL,
    [Created]            DATETIME2 (0)   CONSTRAINT [DF_Payment_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]          DATETIME2 (7)   CONSTRAINT [DF_Payment_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]          BIGINT          NULL,

    CONSTRAINT [PK_Payment] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Payment_Booking] FOREIGN KEY ([BookingId]) REFERENCES [Pay].[Booking] ([Id]),
    CONSTRAINT [FK_Payment_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Payment_Amounts] CHECK ([Amount] >= 0 AND [Fee] >= 0 AND [Total] = [Amount] + [Fee]),
    CONSTRAINT [CK_Payment_Status] CHECK ([Status] BETWEEN 1 AND 5),
    CONSTRAINT [CK_Payment_MethodType] CHECK ([MethodType] IS NULL OR [MethodType] BETWEEN 1 AND 4),
    CONSTRAINT [CK_Payment_AccountLast4] CHECK ([AccountLast4] IS NULL OR [AccountLast4] NOT LIKE '%[^0-9]%')
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Payment_IdempotencyKey]
    ON [Pay].[Payment] ([IdempotencyKey] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Payment_TransactionRef]
    ON [Pay].[Payment] ([TransactionRef] ASC);
GO

-- One gateway transaction pays for one attempt, ever.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Payment_Provider_ProviderTxnId]
    ON [Pay].[Payment] ([Provider] ASC, [ProviderTxnId] ASC)
    WHERE [ProviderTxnId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_Payment_BookingId]
    ON [Pay].[Payment] ([BookingId] ASC)
    INCLUDE ([Status], [Total]);
GO

-- A traveller's payment history, newest first.
CREATE NONCLUSTERED INDEX [IX_Payment_UserId]
    ON [Pay].[Payment] ([UserId] ASC, [Id] DESC)
    INCLUDE ([BookingId], [Status]);
GO

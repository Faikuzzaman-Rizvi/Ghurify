-- Money released from escrow to a host for one trip, in stages.
--
-- Stage: 1 Before departure (a share of each paid seat, a few days before the trip starts),
-- 2 After the trip starts (everything left; the service fee goes to the platform).
-- Amount is the host's share released at this stage; PlatformAmount the fees released with it.
-- The per-booking ledger Release entries are the detail; this row is the host-facing total.
-- Status: 1 Released (out of escrow, waiting for the finance desk to send it), 2 Paid (sent to the
-- host, approved by an admin). (TripId, Stage) is unique: a stage is released once, ever.
CREATE TABLE [Pay].[Payout]
(
    [Id]              BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]          BIGINT          NOT NULL,
    [HostId]          BIGINT          NOT NULL,
    [Stage]           TINYINT         NOT NULL,
    [Amount]          DECIMAL (18, 2) NOT NULL,
    [PlatformAmount]  DECIMAL (18, 2) CONSTRAINT [DF_Payout_PlatformAmount] DEFAULT ((0)) NOT NULL,
    [Status]          TINYINT         CONSTRAINT [DF_Payout_Status] DEFAULT ((1)) NOT NULL,
    [ApprovedById]    BIGINT          NULL,
    [ApprovedOn]      DATETIME2 (0)   NULL,

    [Archived]        BIT             CONSTRAINT [DF_Payout_Archived] DEFAULT ((0)) NOT NULL,
    [Created]         DATETIME2 (0)   CONSTRAINT [DF_Payout_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   CONSTRAINT [DF_Payout_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    CONSTRAINT [PK_Payout] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Payout_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_Payout_User_HostId] FOREIGN KEY ([HostId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Payout_User_ApprovedById] FOREIGN KEY ([ApprovedById]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_Payout_Stage] CHECK ([Stage] >= 1 AND [Stage] <= 2),
    CONSTRAINT [CK_Payout_Status] CHECK ([Status] >= 1 AND [Status] <= 2),
    CONSTRAINT [CK_Payout_Amounts] CHECK ([Amount] >= 0 AND [PlatformAmount] >= 0)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Payout_TripId_Stage]
    ON [Pay].[Payout] ([TripId] ASC, [Stage] ASC);
GO

-- A host's payouts page.
CREATE NONCLUSTERED INDEX [IX_Payout_HostId]
    ON [Pay].[Payout] ([HostId] ASC, [Created] DESC)
    INCLUDE ([Amount], [Status], [Stage]);
GO

-- The admin approval queue.
CREATE NONCLUSTERED INDEX [IX_Payout_Status]
    ON [Pay].[Payout] ([Status] ASC, [Created] ASC);
GO

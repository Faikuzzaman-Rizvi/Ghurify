-- A traveller asking to join a trip, and the host's answer.
--
-- Status: 1 Pending, 2 Approved, 3 Declined, 4 Expired (approved but the seat hold ran out
-- unpaid), 5 Cancelled (withdrawn by the traveller).
-- A traveller has at most one live request per trip (pending or approved); after a decline,
-- expiry or cancellation they may ask again.
CREATE TABLE [Main].[JoinRequest]
(
    [Id]         BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]     BIGINT          NOT NULL,
    [UserId]     BIGINT          NOT NULL,
    [Message]    NVARCHAR (500)  NULL,
    [Status]     TINYINT         CONSTRAINT [DF_JoinRequest_Status] DEFAULT ((1)) NOT NULL,
    [DecidedOn]  DATETIME2 (0)   NULL,

    [Archived]   BIT             CONSTRAINT [DF_JoinRequest_Archived] DEFAULT ((0)) NOT NULL,
    [Created]    DATETIME2 (0)   CONSTRAINT [DF_JoinRequest_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]  DATETIME2 (7)   CONSTRAINT [DF_JoinRequest_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]  BIGINT          NULL,

    CONSTRAINT [PK_JoinRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_JoinRequest_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_JoinRequest_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [CK_JoinRequest_Status] CHECK ([Status] BETWEEN 1 AND 5)
);
GO

-- One live request per traveller per trip.
CREATE UNIQUE NONCLUSTERED INDEX [UX_JoinRequest_TripId_UserId_Active]
    ON [Main].[JoinRequest] ([TripId] ASC, [UserId] ASC)
    WHERE [Status] IN (1, 2) AND [Archived] = 0;
GO

-- The host's "manage requests" page, pending first.
CREATE NONCLUSTERED INDEX [IX_JoinRequest_TripId_Status]
    ON [Main].[JoinRequest] ([TripId] ASC, [Status] ASC)
    INCLUDE ([UserId], [Created]);
GO

-- A traveller's own requests (my trips).
CREATE NONCLUSTERED INDEX [IX_JoinRequest_UserId]
    ON [Main].[JoinRequest] ([UserId] ASC)
    INCLUDE ([TripId], [Status]);
GO

-- A traveller's seat on a trip, from the host's approval to the end of the trip.
--
-- Status: 1 Held (approved, a seat reserved, waiting for payment until HoldExpiresAt),
-- 2 Confirmed (paid into escrow), 3 Cancelled (hold expired, or cancelled before paying),
-- 4 Refunded (paid, then cancelled and refunded).
-- Amount is the trip's price per person at the moment of approval, so a later price change can
-- never alter what this traveller owes.
-- Temporal: every status change is kept in [Pay].[BookingHistory] for support and disputes.
CREATE TABLE [Pay].[Booking]
(
    [Id]             BIGINT          IDENTITY (1, 1) NOT NULL,
    [TripId]         BIGINT          NOT NULL,
    [UserId]         BIGINT          NOT NULL,
    [JoinRequestId]  BIGINT          NOT NULL,
    [Amount]         DECIMAL (18, 2) NOT NULL,
    [Status]         TINYINT         CONSTRAINT [DF_Booking_Status] DEFAULT ((1)) NOT NULL,
    [HoldExpiresAt]  DATETIME2 (0)   NOT NULL,
    [ConfirmedOn]    DATETIME2 (0)   NULL,
    [CancelledOn]    DATETIME2 (0)   NULL,

    [Archived]       BIT             CONSTRAINT [DF_Booking_Archived] DEFAULT ((0)) NOT NULL,
    [Created]        DATETIME2 (0)   CONSTRAINT [DF_Booking_Created] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   CONSTRAINT [DF_Booking_UpdatedOn] DEFAULT (getutcdate()) NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    [SysStartTime]   DATETIME2 (7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [SysEndTime]     DATETIME2 (7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,

    CONSTRAINT [PK_Booking] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Booking_Trip] FOREIGN KEY ([TripId]) REFERENCES [Main].[Trip] ([Id]),
    CONSTRAINT [FK_Booking_User] FOREIGN KEY ([UserId]) REFERENCES [Main].[User] ([Id]),
    CONSTRAINT [FK_Booking_JoinRequest] FOREIGN KEY ([JoinRequestId]) REFERENCES [Main].[JoinRequest] ([Id]),
    CONSTRAINT [CK_Booking_Amount] CHECK ([Amount] >= 0),
    CONSTRAINT [CK_Booking_Status] CHECK ([Status] >= 1 AND [Status] <= 4),
    PERIOD FOR SYSTEM_TIME ([SysStartTime], [SysEndTime])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Pay].[BookingHistory], DATA_CONSISTENCY_CHECK = ON));
GO

-- One booking per join request: approving the same request twice cannot hold two seats.
CREATE UNIQUE NONCLUSTERED INDEX [UX_Booking_JoinRequestId]
    ON [Pay].[Booking] ([JoinRequestId] ASC);
GO

-- The hold-expiry job sweeps held bookings past their deadline every minute.
CREATE NONCLUSTERED INDEX [IX_Booking_Held_HoldExpiresAt]
    ON [Pay].[Booking] ([HoldExpiresAt] ASC)
    INCLUDE ([TripId], [UserId])
    WHERE [Status] = 1;
GO

-- A trip's travellers (group mix, chat membership, payouts).
CREATE NONCLUSTERED INDEX [IX_Booking_TripId_Status]
    ON [Pay].[Booking] ([TripId] ASC, [Status] ASC)
    INCLUDE ([UserId], [Amount]);
GO

-- A traveller's own bookings.
CREATE NONCLUSTERED INDEX [IX_Booking_UserId]
    ON [Pay].[Booking] ([UserId] ASC)
    INCLUDE ([TripId], [Status]);
GO

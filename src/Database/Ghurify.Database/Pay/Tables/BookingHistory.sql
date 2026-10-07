-- History table for [Pay].[Booking]. SQL Server writes to it; nothing else does.
-- When a column is added to Booking.sql it must be added here too, in the same order.
CREATE TABLE [Pay].[BookingHistory]
(
    [Id]             BIGINT          NOT NULL,
    [TripId]         BIGINT          NOT NULL,
    [UserId]         BIGINT          NOT NULL,
    [JoinRequestId]  BIGINT          NOT NULL,
    [Amount]         DECIMAL (18, 2) NOT NULL,
    [Status]         TINYINT         NOT NULL,
    [HoldExpiresAt]  DATETIME2 (0)   NOT NULL,
    [ConfirmedOn]    DATETIME2 (0)   NULL,
    [CancelledOn]    DATETIME2 (0)   NULL,

    [Archived]       BIT             NOT NULL,
    [Created]        DATETIME2 (0)   NOT NULL,
    [UpdatedOn]      DATETIME2 (7)   NOT NULL,
    [UpdatedId]      BIGINT          NULL,

    [SysStartTime]   DATETIME2 (7)   NOT NULL,
    [SysEndTime]     DATETIME2 (7)   NOT NULL
);
GO

CREATE CLUSTERED INDEX [IX_BookingHistory_Period]
    ON [Pay].[BookingHistory] ([SysEndTime] ASC, [SysStartTime] ASC);
GO

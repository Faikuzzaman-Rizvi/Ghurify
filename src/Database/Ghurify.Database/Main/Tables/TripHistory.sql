-- History table for [Main].[Trip]. SQL Server writes to it; nothing else does.
-- When a column is added to Trip.sql it must be added here too, in the same order.
CREATE TABLE [Main].[TripHistory]
(
    [Id]              BIGINT          NOT NULL,
    [HostId]          BIGINT          NOT NULL,
    [DestinationId]   BIGINT          NOT NULL,
    [Title]           NVARCHAR (150)  NOT NULL,
    [Summary]         NVARCHAR (1000) NOT NULL,
    [StartDate]       DATE            NOT NULL,
    [EndDate]         DATE            NOT NULL,
    [MeetingPoint]    NVARCHAR (200)  NOT NULL,
    [Seats]           SMALLINT        NOT NULL,
    [SeatsTaken]      SMALLINT        NOT NULL,
    [PricePerPerson]  DECIMAL (18, 2) NOT NULL,
    [GroupType]       TINYINT         NOT NULL,
    [Status]          TINYINT         NOT NULL,
    [PublishedOn]     DATETIME2 (0)   NULL,

    [Archived]        BIT             NOT NULL,
    [Created]         DATETIME2 (0)   NOT NULL,
    [UpdatedOn]       DATETIME2 (7)   NOT NULL,
    [UpdatedId]       BIGINT          NULL,

    [SysStartTime]    DATETIME2 (7)   NOT NULL,
    [SysEndTime]      DATETIME2 (7)   NOT NULL
);
GO

CREATE CLUSTERED INDEX [IX_TripHistory_Period]
    ON [Main].[TripHistory] ([SysEndTime] ASC, [SysStartTime] ASC);
GO

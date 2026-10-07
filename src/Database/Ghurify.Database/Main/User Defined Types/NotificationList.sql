-- Notifications to store in one call (a trip cancelled tells everyone on it at once).
CREATE TYPE [Main].[NotificationList] AS TABLE
(
    [UserId]     BIGINT          NOT NULL,
    [Kind]       VARCHAR (60)    NOT NULL,
    [Data]       NVARCHAR (1000) NULL,
    [DedupeKey]  VARCHAR (120)   NOT NULL,
    PRIMARY KEY CLUSTERED ([UserId] ASC, [DedupeKey] ASC)
);

-- Refunds to create in one call: one row per booking, with the amount the rules say is owed.
CREATE TYPE [Pay].[RefundRequestList] AS TABLE
(
    [BookingId]  BIGINT          NOT NULL,
    [Amount]     DECIMAL (18, 2) NOT NULL,
    [Reason]     TINYINT         NOT NULL,
    [Reference]  VARCHAR (100)   NOT NULL,
    PRIMARY KEY CLUSTERED ([BookingId] ASC)
);

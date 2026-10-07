-- The gateway's answers to a batch of refunds, recorded in one call.
CREATE TYPE [Pay].[RefundResultList] AS TABLE
(
    [RefundId]           BIGINT          NOT NULL,
    [Succeeded]          BIT             NOT NULL,
    [ProviderRefundRef]  VARCHAR (100)   NULL,
    [FailureReason]      NVARCHAR (300)  NULL,
    PRIMARY KEY CLUSTERED ([RefundId] ASC)
);

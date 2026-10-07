-- Records a gateway callback, once. A second delivery of the same event finds the first and says
-- whether it was already processed, so the caller can answer the gateway without doing anything.
CREATE PROCEDURE [Pay].[AddWebhookEvent]
    @Provider          VARCHAR (20),
    @EventId           VARCHAR (150),
    @TransactionRef    VARCHAR (40),
    @Payload           NVARCHAR (4000),
    @SignatureValid    BIT,
    @Id                BIGINT OUTPUT,
    @AlreadyProcessed  BIT    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @AlreadyProcessed = 0;

    BEGIN TRAN;

    DECLARE @ProcessedOn DATETIME2 (0);

    SELECT @Id          = [Id],
           @ProcessedOn = [ProcessedOn]
    FROM   [Pay].[WebhookEvent] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Provider] = @Provider
      AND  [EventId] = @EventId;

    IF @Id IS NOT NULL
    BEGIN
        SET @AlreadyProcessed = CASE WHEN @ProcessedOn IS NULL THEN 0 ELSE 1 END;
        COMMIT TRAN;
        RETURN;
    END;

    INSERT INTO [Pay].[WebhookEvent] ([Provider], [EventId], [TransactionRef], [Payload], [SignatureValid])
    VALUES (@Provider, @EventId, @TransactionRef, @Payload, @SignatureValid);

    SET @Id = SCOPE_IDENTITY();

    COMMIT TRAN;
END;

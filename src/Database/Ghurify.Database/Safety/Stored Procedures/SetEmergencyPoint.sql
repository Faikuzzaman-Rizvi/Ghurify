-- Adds or edits a police station or hospital shown to people who raise an SOS, near the
-- destination named by @DestinationSlug (or none). With @Id NULL a new point is added, with a
-- generated code. @Checked = 1 records that a person confirmed the details (phone and position)
-- are right, and who.
--
-- Returns the point's id.
CREATE PROCEDURE [Safety].[SetEmergencyPoint]
    @Id             BIGINT         = NULL,
    @DestinationSlug VARCHAR (60)  = NULL,
    @Kind           TINYINT,
    @Name           NVARCHAR (150),
    @NameBn         NVARCHAR (150),
    @Phone          NVARCHAR (20)  = NULL,
    @Latitude       DECIMAL (9, 6),
    @Longitude      DECIMAL (9, 6),
    @Checked        BIT,
    @ActorId        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Location GEOGRAPHY = geography::Point(@Latitude, @Longitude, 4326);
    DECLARE @DestinationId BIGINT = (SELECT [Id] FROM [Main].[Destination] WHERE [Slug] = @DestinationSlug);

    BEGIN TRAN;

    IF @Id IS NULL
    BEGIN
        INSERT INTO [Safety].[EmergencyPoint]
               ([Code], [DestinationId], [Kind], [Name], [NameBn], [Phone], [Location], [CheckedOn], [CheckedById], [UpdatedId])
        VALUES (CONCAT('manual-', CONVERT(VARCHAR (36), NEWID())), @DestinationId, @Kind, @Name, @NameBn, @Phone, @Location,
                CASE WHEN @Checked = 1 THEN SYSUTCDATETIME() END, CASE WHEN @Checked = 1 THEN @ActorId END, @ActorId);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE [Safety].[EmergencyPoint]
        SET    [DestinationId] = @DestinationId, [Kind] = @Kind, [Name] = @Name, [NameBn] = @NameBn, [Phone] = @Phone,
               [Location] = @Location,
               [CheckedOn]   = CASE WHEN @Checked = 1 THEN SYSUTCDATETIME() ELSE NULL END,
               [CheckedById] = CASE WHEN @Checked = 1 THEN @ActorId ELSE NULL END,
               [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId
        WHERE  [Id] = @Id AND [Archived] = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @Id = NULL;
        END;
    END;

    COMMIT TRAN;

    SELECT @Id AS [Id];
END;

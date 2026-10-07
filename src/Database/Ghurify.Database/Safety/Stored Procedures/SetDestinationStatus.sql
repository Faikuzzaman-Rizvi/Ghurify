-- The safety desk changes a destination's status: the destination is updated and the change kept in
-- [Safety].[DestinationAlert], as one transaction. The destination row is locked, so a trip cannot be
-- published to it mid-change (SetTripPublished reads it under the same lock).
--
-- @Result 0 = changed, 1 = no such destination.
CREATE PROCEDURE [Safety].[SetDestinationStatus]
    @Slug           VARCHAR (60),
    @Status         TINYINT,
    @Note           NVARCHAR (300),
    @NoteBn         NVARCHAR (300),
    @ActorId        BIGINT,
    @AlertId        BIGINT  OUTPUT,
    @DestinationId  BIGINT  OUTPUT,
    @Result         TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @AlertId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    SELECT @DestinationId = [Id]
    FROM   [Main].[Destination] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Slug] = @Slug
      AND  [Archived] = 0;

    IF @DestinationId IS NULL
    BEGIN
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[Destination]
    SET    [Status]       = @Status,
           [StatusNote]   = @Note,
           [StatusNoteBn] = @NoteBn,
           [UpdatedOn]    = SYSUTCDATETIME(),
           [UpdatedId]    = @ActorId
    WHERE  [Id] = @DestinationId;

    INSERT INTO [Safety].[DestinationAlert] ([DestinationId], [Status], [Note], [NoteBn], [CreatedById], [UpdatedId])
    VALUES (@DestinationId, @Status, @Note, @NoteBn, @ActorId, @ActorId);

    SET @AlertId = SCOPE_IDENTITY();
    SET @Result = 0;

    COMMIT TRAN;
END;

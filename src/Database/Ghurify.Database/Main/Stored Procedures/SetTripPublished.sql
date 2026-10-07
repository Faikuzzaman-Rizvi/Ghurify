-- Publishes a host's own draft. The destination's status is read under lock in the same
-- transaction, so a trip cannot slip out to a destination the safety desk is closing at that
-- very moment.
--
-- @Result 0 = published, 1 = not found (or not this host's), 2 = not a draft,
-- 3 = the destination is closed.
CREATE PROCEDURE [Main].[SetTripPublished]
    @Id      BIGINT,
    @HostId  BIGINT,
    @Result  TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @DestinationId BIGINT;

    SELECT @Status        = [Status],
           @DestinationId = [DestinationId]
    FROM   [Main].[Trip] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Id] = @Id
      AND  [HostId] = @HostId
      AND  [Archived] = 0;

    IF @Status IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Status <> 1
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF EXISTS (SELECT 1
               FROM   [Main].[Destination] WITH (UPDLOCK, HOLDLOCK)
               WHERE  [Id] = @DestinationId
                 AND  [Status] = 3)
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[Trip]
    SET    [Status]      = 2,
           [PublishedOn] = SYSUTCDATETIME(),
           [UpdatedOn]   = SYSUTCDATETIME(),
           [UpdatedId]   = @HostId
    WHERE  [Id] = @Id;

    SET @Result = 0;

    COMMIT TRAN;
END;

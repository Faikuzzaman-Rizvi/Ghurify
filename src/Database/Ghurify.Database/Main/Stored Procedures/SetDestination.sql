-- Adds a destination, or edits one, by slug. The safety status is not touched here: that has its
-- own audited flow ([Safety].[SetDestinationStatus]), which also closes trips.
--
-- @Result 0 = added, 1 = updated.
CREATE PROCEDURE [Main].[SetDestination]
    @Slug        VARCHAR (60),
    @Name        NVARCHAR (100),
    @NameBn      NVARCHAR (100),
    @Division    NVARCHAR (50),
    @DivisionBn  NVARCHAR (50),
    @Summary     NVARCHAR (400),
    @SummaryBn   NVARCHAR (400),
    @Kind        TINYINT,
    @Latitude    DECIMAL (9, 6) = NULL,
    @Longitude   DECIMAL (9, 6) = NULL,
    @ActorId     BIGINT,
    @Result      TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Location GEOGRAPHY =
        CASE WHEN @Latitude IS NOT NULL AND @Longitude IS NOT NULL THEN geography::Point(@Latitude, @Longitude, 4326) END;

    BEGIN TRAN;

    UPDATE [Main].[Destination] WITH (UPDLOCK, HOLDLOCK)
    SET    [Name] = @Name, [NameBn] = @NameBn, [Division] = @Division, [DivisionBn] = @DivisionBn,
           [Summary] = @Summary, [SummaryBn] = @SummaryBn, [Kind] = @Kind, [Location] = @Location,
           [Archived] = 0, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId
    WHERE  [Slug] = @Slug;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Main].[Destination]
               ([Slug], [Name], [NameBn], [Division], [DivisionBn], [Summary], [SummaryBn], [Kind], [Location], [UpdatedId])
        VALUES (@Slug, @Name, @NameBn, @Division, @DivisionBn, @Summary, @SummaryBn, @Kind, @Location, @ActorId);
        SET @Result = 0;
    END
    ELSE
    BEGIN
        SET @Result = 1;
    END;

    COMMIT TRAN;
END;

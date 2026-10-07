-- Saves changes to a host's own trip: the trip row, and its cost breakdown and itinerary replaced
-- wholesale, as one transaction. Filtered by @HostId, so a host can only ever change their own.
--
-- Once anyone holds a seat, the price and the dates are what they agreed to and are locked; the
-- seat count may grow but never drop below the seats already taken.
--
-- @Result 0 = saved, 1 = not found (or not this host's), 2 = not editable (cancelled or
-- completed), 3 = seats below those taken, 4 = price or dates changed after seats were taken.
CREATE PROCEDURE [Main].[SetTrip]
    @Id              BIGINT,
    @HostId          BIGINT,
    @DestinationId   BIGINT,
    @Title           NVARCHAR (150),
    @Summary         NVARCHAR (1000),
    @StartDate       DATE,
    @EndDate         DATE,
    @MeetingPoint    NVARCHAR (200),
    @Seats           SMALLINT,
    @PricePerPerson  DECIMAL (18, 2),
    @GroupType       TINYINT,
    @CostItems       [Main].[TripCostItemList] READONLY,
    @Days            [Main].[ItineraryDayList] READONLY,
    @Result          TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Status TINYINT, @SeatsTaken SMALLINT, @CurrentPrice DECIMAL (18, 2),
            @CurrentStart DATE, @CurrentEnd DATE, @CurrentDestination BIGINT, @CurrentGroup TINYINT;

    SELECT @Status             = [Status],
           @SeatsTaken         = [SeatsTaken],
           @CurrentPrice       = [PricePerPerson],
           @CurrentStart       = [StartDate],
           @CurrentEnd         = [EndDate],
           @CurrentDestination = [DestinationId],
           @CurrentGroup       = [GroupType]
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

    IF @Status IN (4, 5)
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Seats < @SeatsTaken
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    IF @SeatsTaken > 0
       AND (@PricePerPerson <> @CurrentPrice OR @StartDate <> @CurrentStart OR @EndDate <> @CurrentEnd
            OR @DestinationId <> @CurrentDestination OR @GroupType <> @CurrentGroup)
    BEGIN
        SET @Result = 4;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[Trip]
    SET    [DestinationId]  = @DestinationId,
           [Title]          = @Title,
           [Summary]        = @Summary,
           [StartDate]      = @StartDate,
           [EndDate]        = @EndDate,
           [MeetingPoint]   = @MeetingPoint,
           [Seats]          = @Seats,
           [PricePerPerson] = @PricePerPerson,
           [GroupType]      = @GroupType,
           -- A live trip is Full exactly when every seat is taken.
           [Status]         = CASE WHEN [Status] IN (2, 3)
                                   THEN CASE WHEN [SeatsTaken] >= @Seats THEN 3 ELSE 2 END
                                   ELSE [Status] END,
           [UpdatedOn]      = SYSUTCDATETIME(),
           [UpdatedId]      = @HostId
    WHERE  [Id] = @Id;

    UPDATE [Main].[TripCostItem]
    SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @HostId
    WHERE  [TripId] = @Id AND [Archived] = 0;

    INSERT INTO [Main].[TripCostItem] ([TripId], [Category], [Description], [Amount], [SortOrder], [UpdatedId])
    SELECT @Id, [Category], [Description], [Amount], [SortOrder], @HostId
    FROM   @CostItems;

    UPDATE [Main].[ItineraryDay]
    SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @HostId
    WHERE  [TripId] = @Id AND [Archived] = 0;

    INSERT INTO [Main].[ItineraryDay] ([TripId], [DayNo], [Title], [Details], [Difficulty], [UpdatedId])
    SELECT @Id, [DayNo], [Title], [Details], [Difficulty], @HostId
    FROM   @Days;

    SET @Result = 0;

    COMMIT TRAN;
END;

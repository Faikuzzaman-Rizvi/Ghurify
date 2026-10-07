-- Creates a draft trip with its cost breakdown and itinerary, as one transaction: a trip is never
-- visible, even to its host, with half of its lines missing.
--
-- The caller has already checked the rules (price equals the sum of the lines, dates, seats);
-- this procedure only writes. A new trip is always a Draft; publishing is a separate step.
CREATE PROCEDURE [Main].[AddTrip]
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
    @Id              BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    INSERT INTO [Main].[Trip]
           ([HostId], [DestinationId], [Title], [Summary], [StartDate], [EndDate], [MeetingPoint],
            [Seats], [PricePerPerson], [GroupType], [Status], [UpdatedId])
    VALUES (@HostId, @DestinationId, @Title, @Summary, @StartDate, @EndDate, @MeetingPoint,
            @Seats, @PricePerPerson, @GroupType, 1, @HostId);

    SET @Id = SCOPE_IDENTITY();

    INSERT INTO [Main].[TripCostItem] ([TripId], [Category], [Description], [Amount], [SortOrder], [UpdatedId])
    SELECT @Id, [Category], [Description], [Amount], [SortOrder], @HostId
    FROM   @CostItems;

    INSERT INTO [Main].[ItineraryDay] ([TripId], [DayNo], [Title], [Details], [Difficulty], [UpdatedId])
    SELECT @Id, [DayNo], [Title], [Details], [Difficulty], @HostId
    FROM   @Days;

    COMMIT TRAN;
END;

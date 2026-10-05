-- Sample hosts and trips for showcases and local development.
--
-- Runs only through the "demo" stage (dotnet run -- demo), never in a release: these are made-up
-- people and trips. Dates are relative to the day the script runs, so a fresh demo database
-- always has upcoming trips. Hosts use the reserved demo.ghurify.app domain, so they can never
-- collide with a real sign-up.
--
-- Every insert is guarded, so running the script again on the same database adds nothing.

DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);

-- Hosts. Gender: 1 Female, 2 Male.
INSERT INTO [Main].[User] ([Email], [DisplayName], [Gender], [Status])
SELECT [h].[Email], [h].[DisplayName], [h].[Gender], 1
FROM
(
    VALUES
    (N'nadia.rahman@demo.ghurify.app',    N'Nadia Rahman',    CAST(1 AS TINYINT)),
    (N'tanvir.hasan@demo.ghurify.app',    N'Tanvir Hasan',    CAST(2 AS TINYINT)),
    (N'farhana.akter@demo.ghurify.app',   N'Farhana Akter',   CAST(1 AS TINYINT)),
    (N'rafiq.chowdhury@demo.ghurify.app', N'Rafiq Chowdhury', CAST(2 AS TINYINT))
) AS [h] ([Email], [DisplayName], [Gender])
WHERE NOT EXISTS (SELECT 1 FROM [Main].[User] AS [u] WHERE [u].[Email] = [h].[Email]);

-- Trips. GroupType: 1 Open, 2 WomenOnly, 3 Students, 4 Families.
-- Status: 1 Draft, 2 Published, 3 Full. The draft must NOT appear anywhere public.
DECLARE @Trips TABLE
(
    [Ref]           INT             NOT NULL PRIMARY KEY,
    [HostEmail]     NVARCHAR (256)  NOT NULL,
    [Slug]          VARCHAR (60)    NOT NULL,
    [Title]         NVARCHAR (150)  NOT NULL,
    [Summary]       NVARCHAR (1000) NOT NULL,
    [StartIn]       INT             NOT NULL,
    [Days]          INT             NOT NULL,
    [MeetingPoint]  NVARCHAR (200)  NOT NULL,
    [Seats]         SMALLINT        NOT NULL,
    [SeatsTaken]    SMALLINT        NOT NULL,
    [Price]         DECIMAL (18, 2) NOT NULL,
    [GroupType]     TINYINT         NOT NULL,
    [Status]        TINYINT         NOT NULL
);

INSERT INTO @Trips VALUES
(1, N'tanvir.hasan@demo.ghurify.app', 'sajek', N'Sajek sunrise weekend',
 N'Three days above the clouds. Overnight AC bus to Khagrachari, jeep up to Sajek, sunrise at Konglak Para and a slow evening at Ruilui. Cottage stay with a view of the valley.',
 9, 3, N'Arambagh bus counter, Motijheel, Dhaka', 14, 9, 6800, 1, 2),

(2, N'rafiq.chowdhury@demo.ghurify.app', 'bandarban', N'Nilgiri and Boga Lake trek',
 N'A proper hill trek for fit travellers: Nilgiri viewpoint, a night by Boga Lake with a Bawm family, and a dawn climb with a licensed local guide. Small group, steady pace.',
 16, 4, N'Sayedabad bus terminal, Dhaka', 10, 4, 9500, 1, 2),

(3, N'farhana.akter@demo.ghurify.app', 'coxs-bazar', N'Cox''s Bazar and Inani family escape',
 N'An easy beach break built for families: sea-facing hotel, Marine Drive to Himchari and Inani, and plenty of free time on the sand. Kid-friendly meals throughout.',
 6, 3, N'Kamalapur railway station, Dhaka', 20, 12, 7200, 4, 2),

(4, N'tanvir.hasan@demo.ghurify.app', 'saint-martins', N'Saint Martin''s coral island camp',
 N'Bus to Teknaf, ship across the Bay of Bengal, and two quiet days on the coral island: Chhera Dwip by boat, fresh seafood and a night under the stars.',
 23, 3, N'Arambagh bus counter, Motijheel, Dhaka', 12, 5, 11500, 1, 2),

(5, N'nadia.rahman@demo.ghurify.app', 'sylhet', N'Ratargul, Jaflong and Bichanakandi for women',
 N'A women-only trip led by a verified woman host. Boat through the Ratargul swamp forest, the stone river at Jaflong and the clear water of Bichanakandi. Women-only rooms.',
 12, 3, N'Kamalapur railway station, Dhaka', 12, 7, 7900, 2, 2),

(6, N'farhana.akter@demo.ghurify.app', 'sreemangal', N'Tea gardens and Lawachara rainforest',
 N'A student-budget weekend in the tea capital. Walk the Lawachara trail, cycle through the gardens, and taste the famous seven-layer tea. Almost full.',
 4, 2, N'Kamalapur railway station, Dhaka', 16, 15, 4500, 3, 2),

(7, N'rafiq.chowdhury@demo.ghurify.app', 'tanguar-haor', N'Houseboat night on Tanguar Haor',
 N'Sleep on a wooden houseboat in the middle of the haor, swim at Watchtower, and watch the Meghalaya hills turn blue at dusk. This one sold out fast.',
 19, 2, N'Mohakhali bus terminal, Dhaka', 18, 18, 6200, 1, 3),

(8, N'nadia.rahman@demo.ghurify.app', 'sundarbans', N'Sundarbans mangrove cruise',
 N'Three days on a forest-department approved launch: Kotka, Kochikhali and Harbaria, with armed forest guards on every landing. Look for deer, crocodiles and tiger tracks.',
 30, 3, N'Khulna railway station', 24, 10, 14500, 1, 2),

(9, N'tanvir.hasan@demo.ghurify.app', 'kuakata', N'Kuakata sunrise and sunset',
 N'Overnight launch from Sadarghat, then the one beach in Bangladesh where the sun both rises and sets over the sea. Fatrar Char and the Rakhine village by bike.',
 26, 2, N'Sadarghat launch terminal, Dhaka', 15, 3, 5600, 3, 2),

(10, N'farhana.akter@demo.ghurify.app', 'rangamati', N'Kaptai Lake boat day',
 N'A gentle family trip: a full day on Kaptai Lake, the hanging bridge, Shuvolong waterfall and lunch at a lakeside restaurant run by a local Chakma family.',
 14, 2, N'Sayedabad bus terminal, Dhaka', 14, 6, 4800, 4, 2),

(11, N'nadia.rahman@demo.ghurify.app', 'sajek', N'Sajek for women: cloud-camp weekend',
 N'Women-only, with a woman host and a woman co-lead. Cottage rooms for women only, sunrise at Konglak Para and an evening of local Pahari food.',
 33, 3, N'Arambagh bus counter, Motijheel, Dhaka', 12, 4, 7400, 2, 2),

(12, N'rafiq.chowdhury@demo.ghurify.app', 'bandarban', N'Keokradong summit expedition',
 N'Still a draft: five days to Keokradong and back. Not published, so it must not appear in search.',
 45, 5, N'Sayedabad bus terminal, Dhaka', 8, 0, 13800, 1, 1);

INSERT INTO [Main].[Trip]
       ([HostId], [DestinationId], [Title], [Summary], [StartDate], [EndDate], [MeetingPoint],
        [Seats], [SeatsTaken], [PricePerPerson], [GroupType], [Status], [PublishedOn])
SELECT [u].[Id], [d].[Id], [t].[Title], [t].[Summary],
       DATEADD(DAY, [t].[StartIn], @Today),
       DATEADD(DAY, [t].[StartIn] + [t].[Days] - 1, @Today),
       [t].[MeetingPoint], [t].[Seats], [t].[SeatsTaken], [t].[Price], [t].[GroupType], [t].[Status],
       CASE WHEN [t].[Status] = 1 THEN NULL ELSE SYSUTCDATETIME() END
FROM   @Trips AS [t]
JOIN   [Main].[User]        AS [u] ON [u].[Email] = [t].[HostEmail]
JOIN   [Main].[Destination] AS [d] ON [d].[Slug] = [t].[Slug]
WHERE  NOT EXISTS (SELECT 1
                   FROM   [Main].[Trip] AS [existing]
                   WHERE  [existing].[HostId] = [u].[Id]
                     AND  [existing].[Title] = [t].[Title]);

-- Maps each demo row to the trip it created. Titles are unique per demo host.
DECLARE @TripIds TABLE ([Ref] INT NOT NULL PRIMARY KEY, [TripId] BIGINT NOT NULL);

INSERT INTO @TripIds ([Ref], [TripId])
SELECT [t].[Ref], [trip].[Id]
FROM   @Trips AS [t]
JOIN   [Main].[User] AS [u]    ON [u].[Email] = [t].[HostEmail]
JOIN   [Main].[Trip] AS [trip] ON [trip].[HostId] = [u].[Id] AND [trip].[Title] = [t].[Title];

-- Cost breakdowns. Category: 1 Transport, 2 Stay, 3 Food, 4 Fees, 5 Guide, 6 Buffer.
-- Each trip's lines add up exactly to its price; the check at the end refuses the script if not.
DECLARE @Costs TABLE
(
    [Ref]          INT             NOT NULL,
    [SortOrder]    TINYINT         NOT NULL,
    [Category]     TINYINT         NOT NULL,
    [Description]  NVARCHAR (150)  NOT NULL,
    [Amount]       DECIMAL (18, 2) NOT NULL
);

INSERT INTO @Costs VALUES
(1, 1, 1, N'AC bus Dhaka to Khagrachari and back, shared jeep to Sajek', 2800),
(1, 2, 2, N'Two nights in a valley-view cottage, shared room', 1800),
(1, 3, 3, N'Seven meals, including a bamboo-chicken dinner', 1500),
(1, 4, 4, N'Army checkpoint and Sajek entry fees', 300),
(1, 5, 6, N'Safety buffer for road delays', 400),

(2, 1, 1, N'Bus to Bandarban, chander gari to Ruma and back', 3200),
(2, 2, 2, N'Hotel in town and a Bawm homestay at Boga Lake', 2000),
(2, 3, 3, N'All meals on the trail', 2200),
(2, 4, 5, N'Licensed local guide for three days', 1200),
(2, 5, 4, N'Police and army registration fees', 400),
(2, 6, 6, N'Safety buffer', 500),

(3, 1, 1, N'Train Dhaka to Cox''s Bazar and back, Marine Drive transfer', 2600),
(3, 2, 2, N'Two nights in a sea-facing family room', 2400),
(3, 3, 3, N'Breakfast and dinner, kid-friendly menu', 1600),
(3, 4, 4, N'Himchari park entry', 200),
(3, 5, 6, N'Safety buffer', 400),

(4, 1, 1, N'Bus to Teknaf and return ship to the island', 4500),
(4, 2, 2, N'Two nights in a beach resort, tents for the star night', 3000),
(4, 3, 3, N'All meals, including a seafood barbecue', 2500),
(4, 4, 4, N'Island entry and boat to Chhera Dwip', 800),
(4, 5, 6, N'Buffer for weather delays at sea', 700),

(5, 1, 1, N'Train to Sylhet and back, private microbus', 3000),
(5, 2, 2, N'Two nights in a women-only hotel floor', 2200),
(5, 3, 3, N'All meals', 1700),
(5, 4, 4, N'Ratargul and Bichanakandi boat fees', 500),
(5, 5, 6, N'Safety buffer', 500),

(6, 1, 1, N'Train to Sreemangal and back, bicycle hire', 1800),
(6, 2, 2, N'One night in a tea-garden bungalow', 1200),
(6, 3, 3, N'All meals and a seven-layer tea tasting', 1000),
(6, 4, 5, N'Lawachara forest guide', 300),
(6, 5, 6, N'Safety buffer', 200),

(7, 1, 1, N'Bus to Sunamganj and back, leguna to the ghat', 1800),
(7, 2, 2, N'One night on a wooden houseboat', 2800),
(7, 3, 3, N'Fresh haor fish meals on board', 1200),
(7, 4, 6, N'Safety buffer, life jackets included', 400),

(8, 1, 1, N'AC bus Dhaka to Khulna and back', 2500),
(8, 2, 2, N'Two nights in a cruise cabin on the launch', 6500),
(8, 3, 3, N'All meals on board', 3000),
(8, 4, 4, N'Forest department permits and armed guards', 1500),
(8, 5, 5, N'Naturalist guide', 500),
(8, 6, 6, N'Safety buffer', 500),

(9, 1, 1, N'Launch cabin from Sadarghat and back, bike hire', 2200),
(9, 2, 2, N'One night in a beach hotel', 1500),
(9, 3, 3, N'All meals', 1200),
(9, 4, 6, N'Safety buffer', 700),

(10, 1, 1, N'Bus to Rangamati and back, private lake boat', 2000),
(10, 2, 2, N'One night in a lakeside family room', 1300),
(10, 3, 3, N'All meals, lunch with a Chakma family', 1000),
(10, 4, 4, N'Hanging bridge and park entry', 200),
(10, 5, 6, N'Safety buffer', 300),

(11, 1, 1, N'AC bus Dhaka to Khagrachari and back, women-only jeep', 2800),
(11, 2, 2, N'Two nights, women-only cottage rooms', 2100),
(11, 3, 3, N'Seven meals, Pahari dinner night', 1500),
(11, 4, 4, N'Checkpoint and entry fees', 300),
(11, 5, 6, N'Safety buffer', 700),

(12, 1, 1, N'Bus and chander gari, both ways', 3800),
(12, 2, 2, N'Four nights of homestays', 2500),
(12, 3, 3, N'All meals on the trail', 3000),
(12, 4, 5, N'Licensed guide and porter', 3000),
(12, 5, 4, N'Registration fees', 500),
(12, 6, 6, N'Safety buffer', 1000);

INSERT INTO [Main].[TripCostItem] ([TripId], [Category], [Description], [Amount], [SortOrder])
SELECT [ids].[TripId], [c].[Category], [c].[Description], [c].[Amount], [c].[SortOrder]
FROM   @Costs   AS [c]
JOIN   @TripIds AS [ids] ON [ids].[Ref] = [c].[Ref]
WHERE  NOT EXISTS (SELECT 1 FROM [Main].[TripCostItem] AS [x] WHERE [x].[TripId] = [ids].[TripId]);

-- Day-by-day plans. Difficulty: 1 Easy, 2 Moderate, 3 Challenging.
DECLARE @Days TABLE
(
    [Ref]         INT             NOT NULL,
    [DayNo]       TINYINT         NOT NULL,
    [Title]       NVARCHAR (150)  NOT NULL,
    [Details]     NVARCHAR (1000) NOT NULL,
    [Difficulty]  TINYINT         NOT NULL
);

INSERT INTO @Days VALUES
(1, 1, N'Into the hills', N'Arrive in Khagrachari at dawn, breakfast, then the jeep convoy up to Sajek with the army escort. Afternoon at Ruilui Para and sunset at the helipad.', 1),
(1, 2, N'Sunrise over the clouds', N'Wake at 5 for sunrise at Konglak Para, a short but steep walk. Free afternoon, bamboo-chicken dinner and stories by the fire.', 2),
(1, 3, N'Back to Dhaka', N'Slow breakfast with the valley view, jeep down to Khagrachari, Alutila cave on the way, and the overnight bus home.', 1),

(2, 1, N'Bandarban town and Nilgiri', N'Arrive in the morning, register with the police, and drive the hill road to Nilgiri for sunset above the clouds.', 1),
(2, 2, N'To Ruma and Boga Lake', N'Chander gari to Ruma, register with the army camp, then the climb to Boga Lake. Night with a Bawm family by the lake.', 3),
(2, 3, N'Keokradong dawn climb', N'An early climb towards Keokradong with the guide, back to Boga Lake for lunch, and an easy afternoon.', 3),
(2, 4, N'Return', N'Walk down to Ruma, drive back to Bandarban, and the evening bus to Dhaka.', 2),

(3, 1, N'Beach arrival', N'Morning train arrives, check in to the sea-facing hotel, and an afternoon on Laboni beach.', 1),
(3, 2, N'Marine Drive day', N'Drive the Marine Drive to Himchari waterfall and Inani''s rocky beach. Back for a seafood dinner.', 1),
(3, 3, N'Free morning and home', N'Free morning on the beach, Burmese market for souvenirs, and the afternoon train home.', 1),

(4, 1, N'Teknaf and the ship', N'Overnight bus to Teknaf, ship across the bay, check in and swim on the west beach.', 2),
(4, 2, N'Chhera Dwip', N'Boat to Chhera Dwip, the southern tip of Bangladesh. Seafood barbecue and a night under the stars.', 2),
(4, 3, N'Back to the mainland', N'Morning walk around the island and the afternoon ship back to Teknaf for the night bus.', 1),

(5, 1, N'Ratargul swamp forest', N'Morning train, women-only microbus to Ratargul and a boat through the flooded forest.', 1),
(5, 2, N'Jaflong', N'The stone river at Jaflong, the Khasia village and the tea gardens on the way back.', 2),
(5, 3, N'Bichanakandi and home', N'Boat to Bichanakandi''s clear water, lunch by the river, and the evening train to Dhaka.', 1),

(6, 1, N'Lawachara and the gardens', N'Morning train, the Lawachara forest trail with a guide, and an afternoon cycling through the tea gardens.', 2),
(6, 2, N'Seven-layer tea and home', N'Madhabpur Lake at sunrise, the famous seven-layer tea, and the afternoon train home.', 1),

(7, 1, N'Onto the haor', N'Bus to Sunamganj, board the houseboat, swim at the watchtower and dinner on deck as the hills turn blue.', 1),
(7, 2, N'Niladri Lake and home', N'Sunrise on the water, Niladri Lake and the Barik Tila viewpoint, then the night bus home.', 1),

(8, 1, N'Into the mangroves', N'Board the launch at Khulna, sail past Mongla into the forest, and a canopy walk at Harbaria.', 1),
(8, 2, N'Kotka and Kochikhali', N'Dawn boat through the creeks looking for deer and birds, a guarded walk to Kochikhali beach.', 2),
(8, 3, N'Karamjal and return', N'The crocodile breeding centre at Karamjal and the slow cruise back to Khulna.', 1),

(9, 1, N'The launch and the beach', N'Arrive by launch, bikes to the beach, and sunset straight out over the sea.', 1),
(9, 2, N'Sunrise and Fatrar Char', N'Sunrise from the eastern beach, boat to Fatrar Char, the Rakhine village, and the evening launch home.', 1),

(10, 1, N'Kaptai Lake', N'Bus to Rangamati, a private boat to Shuvolong waterfall and lunch with a Chakma family.', 1),
(10, 2, N'Hanging bridge and home', N'The hanging bridge, the tribal cultural museum and the afternoon bus home.', 1),

(11, 1, N'Up to Sajek', N'Women-only jeep convoy up to Sajek, check in and sunset at the helipad.', 1),
(11, 2, N'Konglak Para sunrise', N'Sunrise walk to Konglak Para, a free afternoon and a Pahari food night.', 2),
(11, 3, N'Home', N'Breakfast with the valley view and the drive back to Khagrachari for the bus.', 1),

(12, 1, N'To Ruma', N'Draft itinerary.', 2);

INSERT INTO [Main].[ItineraryDay] ([TripId], [DayNo], [Title], [Details], [Difficulty])
SELECT [ids].[TripId], [d].[DayNo], [d].[Title], [d].[Details], [d].[Difficulty]
FROM   @Days    AS [d]
JOIN   @TripIds AS [ids] ON [ids].[Ref] = [d].[Ref]
WHERE  NOT EXISTS (SELECT 1 FROM [Main].[ItineraryDay] AS [x] WHERE [x].[TripId] = [ids].[TripId]);

-- The pricing promise: every demo trip's cost lines add up to its price. If someone edits the
-- numbers above and gets it wrong, the whole script rolls back rather than ship a bad demo.
IF EXISTS
(
    SELECT 1
    FROM   @TripIds AS [ids]
    JOIN   [Main].[Trip] AS [trip] ON [trip].[Id] = [ids].[TripId]
    WHERE  [trip].[PricePerPerson] <> (SELECT ISNULL(SUM([c].[Amount]), 0)
                                       FROM   [Main].[TripCostItem] AS [c]
                                       WHERE  [c].[TripId] = [trip].[Id]
                                         AND  [c].[Archived] = 0)
)
    THROW 50001, 'Demo data: a trip''s cost items do not add up to its price.', 1;

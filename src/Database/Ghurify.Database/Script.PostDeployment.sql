/*
Post-deployment script. Runs after every dacpac publish, on every environment.

Rules:
  - Seed and lookup rows only. No schema changes (those are table files), no data fixes
    (those are DbUp scripts in src/Ghurify.DatabaseUpdate).
  - Every statement is insert-if-missing so re-running the deploy changes nothing.
  - Never delete or overwrite rows a user may have edited.
*/

-- Destinations. Insert-if-missing by slug: once a row exists, its status and text belong to
-- the safety desk and the admin screens, and a redeploy must never reset them.
-- Kind: 1 Hills, 2 Beach, 3 Island, 4 Forest, 5 Wetland, 6 TeaGarden, 7 Lake, 8 River.
-- Status: 1 Open, 2 Caution, 3 Closed.
INSERT INTO [Main].[Destination]
       ([Slug], [Name], [NameBn], [Division], [DivisionBn], [Summary], [SummaryBn], [Kind],
        [Location], [Status], [StatusNote], [StatusNoteBn])
SELECT [seed].[Slug], [seed].[Name], [seed].[NameBn], [seed].[Division], [seed].[DivisionBn],
       [seed].[Summary], [seed].[SummaryBn], [seed].[Kind],
       geography::Point([seed].[Latitude], [seed].[Longitude], 4326),
       [seed].[Status], [seed].[StatusNote], [seed].[StatusNoteBn]
FROM
(
    VALUES
    ('sajek', N'Sajek Valley', N'সাজেক ভ্যালি', N'Chattogram', N'চট্টগ্রাম',
     N'A ridge-top village in the Rangamati hills, famous for sunrise over a sea of clouds.',
     N'রাঙ্গামাটির পাহাড়চূড়ার গ্রাম, মেঘের সমুদ্রের ওপর সূর্যোদয়ের জন্য বিখ্যাত।',
     1, 23.3820, 92.2938, 1, NULL, NULL),

    ('bandarban', N'Bandarban', N'বান্দরবান', N'Chattogram', N'চট্টগ্রাম',
     N'The highest hills in the country: Nilgiri, Boga Lake and the trail to Keokradong.',
     N'দেশের সবচেয়ে উঁচু পাহাড়: নীলগিরি, বগা লেক আর কেওক্রাডংয়ের পথ।',
     1, 22.1953, 92.2184, 1, NULL, NULL),

    ('coxs-bazar', N'Cox''s Bazar', N'কক্সবাজার', N'Chattogram', N'চট্টগ্রাম',
     N'The longest natural sea beach in the world, with Himchari and Inani close by.',
     N'পৃথিবীর দীর্ঘতম প্রাকৃতিক সমুদ্রসৈকত, কাছেই হিমছড়ি আর ইনানী।',
     2, 21.4272, 92.0058, 1, NULL, NULL),

    ('saint-martins', N'Saint Martin''s Island', N'সেন্ট মার্টিন দ্বীপ', N'Chattogram', N'চট্টগ্রাম',
     N'The only coral island of Bangladesh: clear water, coconut groves and quiet nights.',
     N'বাংলাদেশের একমাত্র প্রবাল দ্বীপ: স্বচ্ছ পানি, নারকেল বাগান আর নিরিবিলি রাত।',
     3, 20.6237, 92.3234, 2,
     N'Visitor limits apply this season. Check the latest notice before you travel.',
     N'এই মৌসুমে পর্যটকের সংখ্যা সীমিত। যাত্রার আগে সর্বশেষ নির্দেশনা দেখে নিন।'),

    ('sylhet', N'Sylhet', N'সিলেট', N'Sylhet', N'সিলেট',
     N'Stone rivers at Jaflong, the Ratargul swamp forest and the waters of Bichanakandi.',
     N'জাফলংয়ের পাথুরে নদী, রাতারগুল জলাবন আর বিছানাকান্দির স্বচ্ছ জল।',
     8, 24.8949, 91.8687, 1, NULL, NULL),

    ('sreemangal', N'Sreemangal', N'শ্রীমঙ্গল', N'Sylhet', N'সিলেট',
     N'The tea capital: rolling gardens, the Lawachara rainforest and seven-layer tea.',
     N'চায়ের রাজধানী: ঢেউ খেলানো চা বাগান, লাউয়াছড়া বন আর সাত রঙের চা।',
     6, 24.3065, 91.7296, 1, NULL, NULL),

    ('tanguar-haor', N'Tanguar Haor', N'টাঙ্গুয়ার হাওর', N'Sylhet', N'সিলেট',
     N'A vast seasonal wetland. Spend a night on a houseboat under the stars.',
     N'বিশাল মৌসুমি জলাভূমি। তারাভরা আকাশের নিচে হাউসবোটে একটি রাত।',
     5, 25.1500, 91.0833, 1, NULL, NULL),

    ('sundarbans', N'Sundarbans', N'সুন্দরবন', N'Khulna', N'খুলনা',
     N'The largest mangrove forest on earth and home of the Royal Bengal tiger.',
     N'পৃথিবীর বৃহত্তম ম্যানগ্রোভ বন, রয়েল বেঙ্গল টাইগারের আবাস।',
     4, 21.9497, 89.1833, 1, NULL, NULL),

    ('kuakata', N'Kuakata', N'কুয়াকাটা', N'Barishal', N'বরিশাল',
     N'The beach where you can watch both sunrise and sunset over the sea.',
     N'যে সৈকতে সমুদ্রের ওপর সূর্যোদয় আর সূর্যাস্ত দুটোই দেখা যায়।',
     2, 21.8167, 90.1167, 1, NULL, NULL),

    ('rangamati', N'Rangamati', N'রাঙ্গামাটি', N'Chattogram', N'চট্টগ্রাম',
     N'Kaptai Lake, the hanging bridge and boat rides between green islands.',
     N'কাপ্তাই লেক, ঝুলন্ত সেতু আর সবুজ দ্বীপের মাঝে নৌকাভ্রমণ।',
     7, 22.6533, 92.1750, 1, NULL, NULL)
) AS [seed] ([Slug], [Name], [NameBn], [Division], [DivisionBn], [Summary], [SummaryBn], [Kind],
             [Latitude], [Longitude], [Status], [StatusNote], [StatusNoteBn])
WHERE NOT EXISTS (SELECT 1 FROM [Main].[Destination] AS [d] WHERE [d].[Slug] = [seed].[Slug]);

PRINT 'Post-deployment: destinations seeded (insert-if-missing).';

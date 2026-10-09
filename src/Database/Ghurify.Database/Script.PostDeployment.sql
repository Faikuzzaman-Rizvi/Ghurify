/*
Post-deployment script. Runs after every dacpac publish, on every environment.

Rules:
  - Seed and lookup rows only. No schema changes (those are table files), no data fixes
    (those are DbUp scripts in src/Ghurify.DatabaseUpdate).
  - Every statement is insert-if-missing so re-running the deploy changes nothing.
  - Never delete or overwrite rows a user may have edited.
*/

-- The staff roles the platform ships with, and what each may do. Insert-if-missing by key:
-- once a role exists its permissions belong to the super admin who edits them from the portal,
-- and a redeploy must never reset them. A later release that adds a permission grants it to
-- these roles with a DbUp data script, not from here.
--
-- super-admin keeps no permission rows on purpose: it holds every permission there is, now and
-- in later releases (see Ghurify.Domain.Identity.UserAccess.Can).
INSERT INTO [Main].[StaffRole] ([Key], [Name], [NameBn], [Description], [DescriptionBn], [IsSystem], [IsSuperAdmin])
SELECT [seed].[Key], [seed].[Name], [seed].[NameBn], [seed].[Description], [seed].[DescriptionBn],
       1, [seed].[IsSuperAdmin]
FROM
(
    VALUES
    ('super-admin', N'Super admin', N'সুপার অ্যাডমিন',
     N'Complete control of the platform, including who else is on the admin desk and what the site looks like.',
     N'প্ল্যাটফর্মের সম্পূর্ণ নিয়ন্ত্রণ, কে অ্যাডমিন ডেস্কে থাকবে আর সাইট কেমন দেখাবে তা-ও।', CAST(1 AS BIT)),

    ('admin', N'Admin', N'অ্যাডমিন',
     N'Runs the platform day to day: people, trips, money, safety and moderation.',
     N'প্রতিদিনের প্ল্যাটফর্ম পরিচালনা: মানুষ, ট্রিপ, টাকা, নিরাপত্তা আর মডারেশন।', CAST(0 AS BIT)),

    ('moderator', N'Moderator', N'মডারেটর',
     N'Reports, disputes and the stories people post.',
     N'রিপোর্ট, বিরোধ আর মানুষের পোস্ট করা গল্প।', CAST(0 AS BIT)),

    ('safety-desk', N'Safety desk', N'সেফটি ডেস্ক',
     N'SOS alerts, check-ins, destination closures and emergency points.',
     N'এসওএস সতর্কতা, চেক-ইন, গন্তব্য বন্ধ করা আর ইমার্জেন্সি পয়েন্ট।', CAST(0 AS BIT))
) AS [seed] ([Key], [Name], [NameBn], [Description], [DescriptionBn], [IsSuperAdmin])
WHERE NOT EXISTS (SELECT 1 FROM [Main].[StaffRole] AS [r] WHERE [r].[Key] = [seed].[Key] AND [r].[Archived] = 0);

-- The starting permissions of the three ordinary system roles. Each set matches exactly what
-- that role could do before roles became data, so nobody gained or lost access on the upgrade.
INSERT INTO [Main].[StaffRolePermission] ([StaffRoleId], [Permission])
SELECT [r].[Id], [seed].[Permission]
FROM
(
    VALUES
    ('admin', 'dashboard.view'),
    ('admin', 'users.view'),
    ('admin', 'users.suspend'),
    ('admin', 'users.security'),
    ('admin', 'users.roles.manage'),
    ('admin', 'users.verify'),
    ('admin', 'users.documents.view'),
    ('admin', 'users.avatar.remove'),
    ('admin', 'trips.view'),
    ('admin', 'trips.cancel'),
    ('admin', 'destinations.manage'),
    ('admin', 'bookings.view'),
    ('admin', 'payments.view'),
    ('admin', 'payments.refund.retry'),
    ('admin', 'payouts.view'),
    ('admin', 'payouts.approve'),
    ('admin', 'safety.sos.view'),
    ('admin', 'safety.sos.manage'),
    ('admin', 'safety.checkins.view'),
    ('admin', 'safety.destinations.status'),
    ('admin', 'safety.points.manage'),
    ('admin', 'moderation.reports.view'),
    ('admin', 'moderation.reports.resolve'),
    ('admin', 'moderation.content.manage'),
    ('admin', 'moderation.disputes'),
    ('admin', 'staff.view'),
    ('admin', 'audit.view'),

    ('moderator', 'dashboard.view'),
    ('moderator', 'moderation.reports.view'),
    ('moderator', 'moderation.reports.resolve'),
    ('moderator', 'moderation.content.manage'),
    ('moderator', 'users.avatar.remove'),

    ('safety-desk', 'dashboard.view'),
    ('safety-desk', 'safety.sos.view'),
    ('safety-desk', 'safety.sos.manage'),
    ('safety-desk', 'safety.checkins.view'),
    ('safety-desk', 'safety.destinations.status'),
    ('safety-desk', 'safety.points.manage')
) AS [seed] ([Key], [Permission])
JOIN [Main].[StaffRole] AS [r] ON [r].[Key] = [seed].[Key] AND [r].[Archived] = 0
-- Only fills in a role that has no permissions at all, i.e. one this script just created. A
-- super admin who deliberately strips a permission from the admin role must not get it back on
-- the next deploy.
WHERE NOT EXISTS (SELECT 1 FROM [Main].[StaffRolePermission] AS [p] WHERE [p].[StaffRoleId] = [r].[Id]);

PRINT 'Post-deployment: staff roles seeded (insert-if-missing).';

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

-- Police stations and hospitals near each destination, for "nearest help" on the SOS screen.
-- Insert-if-missing by code; the safety desk owns the rows after that. Positions are the local
-- upazila or district town and are APPROXIMATE: the safety desk must confirm each one (and add
-- phone numbers) before launch. The SOS screen always offers the national emergency number 999.
-- Kind: 1 Police, 2 Hospital, 3 Tourist police.
INSERT INTO [Safety].[EmergencyPoint] ([Code], [DestinationId], [Kind], [Name], [NameBn], [Location])
SELECT [seed].[Code], [d].[Id], [seed].[Kind], [seed].[Name], [seed].[NameBn],
       geography::Point([seed].[Latitude], [seed].[Longitude], 4326)
FROM
(
    VALUES
    ('sajek-police',         'sajek',         1, N'Baghaichhari Police Station',             N'বাঘাইছড়ি থানা',                     23.2333, 92.1667),
    ('sajek-hospital',       'sajek',         2, N'Baghaichhari Upazila Health Complex',     N'বাঘাইছড়ি উপজেলা স্বাস্থ্য কমপ্লেক্স', 23.2340, 92.1680),
    ('bandarban-police',     'bandarban',     1, N'Bandarban Sadar Police Station',          N'বান্দরবান সদর থানা',                 22.1953, 92.2184),
    ('bandarban-hospital',   'bandarban',     2, N'Bandarban District Sadar Hospital',       N'বান্দরবান জেলা সদর হাসপাতাল',         22.1960, 92.2200),
    ('coxs-tourist-police',  'coxs-bazar',    3, N'Tourist Police, Cox''s Bazar Zone',       N'ট্যুরিস্ট পুলিশ, কক্সবাজার জোন',         21.4272, 92.0058),
    ('coxs-hospital',        'coxs-bazar',    2, N'Cox''s Bazar District Sadar Hospital',    N'কক্সবাজার জেলা সদর হাসপাতাল',         21.4440, 91.9800),
    ('saint-martins-police', 'saint-martins', 1, N'Saint Martin''s Police Outpost',          N'সেন্ট মার্টিন পুলিশ ফাঁড়ি',              20.6237, 92.3234),
    ('teknaf-hospital',      'saint-martins', 2, N'Teknaf Upazila Health Complex',           N'টেকনাফ উপজেলা স্বাস্থ্য কমপ্লেক্স',      20.8640, 92.3010),
    ('sylhet-police',        'sylhet',        1, N'Gowainghat Police Station',               N'গোয়াইনঘাট থানা',                      25.1000, 91.9330),
    ('sylhet-hospital',      'sylhet',        2, N'Sylhet MAG Osmani Medical College Hospital', N'সিলেট এম এ জি ওসমানী মেডিকেল কলেজ হাসপাতাল', 24.9000, 91.8530),
    ('sreemangal-police',    'sreemangal',    1, N'Sreemangal Police Station',               N'শ্রীমঙ্গল থানা',                       24.3065, 91.7296),
    ('sreemangal-hospital',  'sreemangal',    2, N'Sreemangal Upazila Health Complex',       N'শ্রীমঙ্গল উপজেলা স্বাস্থ্য কমপ্লেক্স',    24.3070, 91.7310),
    ('tanguar-police',       'tanguar-haor',  1, N'Tahirpur Police Station',                 N'তাহিরপুর থানা',                        25.0830, 91.1670),
    ('tanguar-hospital',     'tanguar-haor',  2, N'Tahirpur Upazila Health Complex',         N'তাহিরপুর উপজেলা স্বাস্থ্য কমপ্লেক্স',     25.0840, 91.1690),
    ('sundarbans-police',    'sundarbans',    1, N'Mongla Police Station',                   N'মোংলা থানা',                            22.4800, 89.6000),
    ('sundarbans-hospital',  'sundarbans',    2, N'Mongla Upazila Health Complex',           N'মোংলা উপজেলা স্বাস্থ্য কমপ্লেক্স',       22.4810, 89.6010),
    ('kuakata-tourist-police','kuakata',      3, N'Tourist Police, Kuakata',                 N'ট্যুরিস্ট পুলিশ, কুয়াকাটা',               21.8167, 90.1167),
    ('kuakata-hospital',     'kuakata',       2, N'Kalapara Upazila Health Complex',         N'কলাপাড়া উপজেলা স্বাস্থ্য কমপ্লেক্স',      21.9900, 90.2400),
    ('rangamati-police',     'rangamati',     1, N'Rangamati Kotwali Police Station',        N'রাঙ্গামাটি কোতোয়ালী থানা',               22.6533, 92.1750),
    ('rangamati-hospital',   'rangamati',     2, N'Rangamati General Hospital',              N'রাঙ্গামাটি জেনারেল হাসপাতাল',            22.6540, 92.1770)
) AS [seed] ([Code], [Slug], [Kind], [Name], [NameBn], [Latitude], [Longitude])
JOIN [Main].[Destination] AS [d] ON [d].[Slug] = [seed].[Slug]
WHERE NOT EXISTS (SELECT 1 FROM [Safety].[EmergencyPoint] AS [e] WHERE [e].[Code] = [seed].[Code]);

PRINT 'Post-deployment: emergency points seeded (insert-if-missing).';

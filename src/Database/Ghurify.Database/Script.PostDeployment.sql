/*
Post-deployment script. Runs after every dacpac publish, on every environment.

Rules:
  - Seed and lookup rows only. No schema changes (those are table files), no data fixes
    (those are DbUp scripts in src/Ghurify.DatabaseUpdate).
  - Every statement is insert-if-missing so re-running the deploy changes nothing.
  - Never delete or overwrite rows a user may have edited.

Pattern to follow:

    IF NOT EXISTS (SELECT 1 FROM [Main].[Destination] WHERE [Slug] = N'sajek')
        INSERT INTO [Main].[Destination] ([Name], [Slug], [Status])
        VALUES (N'Sajek Valley', N'sajek', 1);

No seed rows yet: destinations arrive with the Trips feature.
*/

PRINT 'Post-deployment: no seed data to apply.';

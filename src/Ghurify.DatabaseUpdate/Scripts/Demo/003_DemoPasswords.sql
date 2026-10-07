-- A password for every demo account, now that sign-in is by email and password. Runs only
-- through the "demo" stage, never in a release.
--
-- Every @demo.ghurify.app account gets the same password: Ghurify-demo-2026
-- (PBKDF2-HMAC-SHA256, 600,000 iterations, one fixed salt, computed ahead of time). A shared,
-- published password is fine for throwaway demo data and nowhere else.
--
-- Guarded, so running the script again changes nothing, and an account whose password was
-- already changed in the app keeps it.

INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations], [Algorithm])
SELECT [u].[Id],
       0x5411FA725B552DC7A4B41C5D28451BD4A02D7BAB1CEFC52C0361A039EAB1B8E5,
       0x6768757269667920646D6F2073616C74,
       600000,
       1
FROM   [Main].[User] AS [u]
WHERE  [u].[Email] LIKE '%@demo.ghurify.app'
  AND  NOT EXISTS (SELECT 1 FROM [Main].[UserCredential] AS [c] WHERE [c].[UserId] = [u].[Id]);

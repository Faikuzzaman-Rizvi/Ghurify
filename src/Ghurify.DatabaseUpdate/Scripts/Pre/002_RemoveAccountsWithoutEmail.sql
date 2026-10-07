-- Sign-in moved from phone number to email address.
--
-- Accounts created under the old design have no email, so after the change they can never
-- sign in again: there is no address to send a code to, and no way to derive one from a
-- phone number. They are dead rows, and Email is about to become NOT NULL, which a row
-- without one would block.
--
-- This runs in Scripts/Pre because it deletes data, which BlockOnPossibleDataLoss would
-- otherwise stop at the dacpac step. That is the point of Scripts/Pre: the loss is
-- deliberate and reviewed, not a surprise.
--
-- The guard below is what makes this safe to leave in the repository forever. It only does
-- anything while [Main].[User] still lacks an [Email] column, which is true exactly once,
-- on the deploy that introduces it. On every later run the column exists and this is a
-- no-op, so it can never delete a real account. On a brand-new database there is no table at
-- all yet, which is also a no-op (the first check below).

IF OBJECT_ID(N'[Main].[User]', N'U') IS NOT NULL AND COL_LENGTH('Main.User', 'Email') IS NULL
BEGIN
    PRINT 'Pre-schema: moving sign-in to email. Removing pre-migration accounts that have no address.';

    -- Order matters: RefreshToken has a foreign key to User.
    DELETE FROM [Main].[RefreshToken];

    -- Old codes were keyed by phone number; that column is about to be replaced.
    DELETE FROM [Main].[OtpCode];

    -- [Main].[User] is system-versioned, so deleting from it COPIES every row into
    -- [Main].[UserHistory]. The dacpac then cannot add a NOT NULL [Email] column to a
    -- history table that has rows, and the publish fails.
    --
    -- A history table cannot be emptied while versioning is on, so versioning is switched
    -- off for exactly these two deletes and switched straight back on. That is an ALTER in
    -- a folder that is otherwise data-only: it is a deliberate, scoped exception, it is the
    -- only way to clear history, and the table ends in exactly the state it started in.
    -- The script runs in one transaction, so a failure rolls the ALTER back too.
    ALTER TABLE [Main].[User] SET (SYSTEM_VERSIONING = OFF);

    DELETE FROM [Main].[User];

    -- Deliberately dynamic. SQL Server compiles a whole batch before running any of it, and
    -- at compile time [Main].[UserHistory] is still a history table, so a plain DELETE here
    -- is rejected outright (error 13560) even though the ALTER above has already run by the
    -- time it would execute. EXEC defers compilation until versioning is actually off.
    EXEC (N'DELETE FROM [Main].[UserHistory];');

    ALTER TABLE [Main].[User]
        SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Main].[UserHistory],
                                     DATA_CONSISTENCY_CHECK = ON));
END
ELSE
BEGIN
    PRINT 'Pre-schema: [Main].[User].[Email] already exists. Nothing to do.';
END;

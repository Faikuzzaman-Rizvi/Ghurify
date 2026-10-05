/*
Pre-deployment script. Runs before the schema diff, as part of every dacpac publish.

Why this file exists:
  The deploy runs with DropObjectsNotInSource=false, so it never deletes the Hangfire and
  DbUp tables, which live outside this project. The cost of that setting is that an object
  REMOVED or RENAMED here is simply left behind on the server, forever, as dead code that
  still compiles against the schema. This is where those are retired, deliberately.

Rules:
  - Drops only. No data changes (those are DbUp scripts) and no creates (those are the
    object files themselves).
  - Always guard with an existence check, so the script is safe on a fresh database and on
    every later deploy.
  - Remove an entry once every environment has run it. Leaving them costs nothing but noise.
*/

-- Renamed to [Main].[GetOrAddUserByEmail] when sign-in moved from phone number to email.
IF OBJECT_ID('[Main].[GetOrAddUserByPhone]', 'P') IS NOT NULL
BEGIN
    PRINT 'Pre-deployment: dropping [Main].[GetOrAddUserByPhone], renamed to [Main].[GetOrAddUserByEmail].';
    DROP PROCEDURE [Main].[GetOrAddUserByPhone];
END;

-- Sample data script.
--
-- Scripts in Scripts/{Year} run AFTER the dacpac is published, against the NEW schema.
-- Use them to backfill or correct existing rows, for example:
--
--   UPDATE [Main].[Trip]
--   SET    [PricePerPerson] = c.[Total]
--   FROM   [Main].[Trip] t
--   CROSS APPLY (SELECT SUM([Amount]) AS [Total]
--                FROM [Main].[TripCostItem]
--                WHERE [TripId] = t.[Id]) c
--   WHERE  t.[PricePerPerson] IS NULL;
--
-- Never edit a script after it has run in production. Write a new one.

PRINT 'Data: nothing to do.';

-- Sample pre-schema script.
--
-- Scripts in Scripts/Pre run BEFORE the dacpac is published, against the OLD schema.
-- Use them only to remove or move data that BlockOnPossibleDataLoss=true would otherwise
-- block, for example clearing a column that is about to be dropped.
--
-- Data only. No CREATE, ALTER or DROP of schema objects.
-- Schema-qualified, no GO, idempotent where possible.

PRINT 'Pre-schema: nothing to do.';

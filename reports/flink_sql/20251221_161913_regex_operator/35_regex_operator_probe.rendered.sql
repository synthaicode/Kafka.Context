-- Probe: regex predicate operators (alternative to SIMILAR TO)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  ('abc-123' REGEXP '^[a-z]+-[0-9]+$') AS RegexpOp;


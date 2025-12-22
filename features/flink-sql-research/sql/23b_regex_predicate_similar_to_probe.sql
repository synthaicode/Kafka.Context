-- Probe: regex-like predicate operators (this Flink image may not support them)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  ('abc-123' SIMILAR TO '[a-z]+-[0-9]+') AS SimilarToOk;


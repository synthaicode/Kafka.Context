-- Probe: Regex patterns (useful when LINQ uses string.Contains with complex patterns)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  REGEXP_LIKE('abc-123', '^[a-z]+-[0-9]+$') AS RegexLikeOk,
  REGEXP_EXTRACT('abc-123', '([a-z]+)-([0-9]+)', 1) AS RegexExtract1,
  REGEXP_EXTRACT('abc-123', '([a-z]+)-([0-9]+)', 2) AS RegexExtract2,
  REGEXP_REPLACE('a---b', '-+', '-') AS RegexReplace;


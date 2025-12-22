-- Flink: LIKE escape (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  'a_b' LIKE 'a\\_b' ESCAPE '\\' AS LikeLiteralUnderscore,
  'a%b' LIKE 'a\\%b' ESCAPE '\\' AS LikeLiteralPercent,
  'a_b' LIKE 'a_b' AS LikeWildcardUnderscore,
  'a%b' LIKE 'a%b' AS LikeWildcardPercent;


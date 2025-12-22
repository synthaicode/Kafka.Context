-- Flink: ARRAY helper function names (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  ARRAY[10, 20, 30] AS Arr,
  ARRAY_CONTAINS(ARRAY[10, 20, 30], 20) AS Contains20,
  ARRAY_DISTINCT(ARRAY[10, 20, 20, 30]) AS Distincted,
  ARRAY_JOIN(ARRAY['a','b','c'], '-') AS Joined;


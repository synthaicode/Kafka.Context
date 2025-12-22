-- Flink: MAP keys/values function names (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  MAP['a', 1, 'b', 2] AS M,
  MAP_KEYS(MAP['a', 1, 'b', 2]) AS Keys,
  MAP_VALUES(MAP['a', 1, 'b', 2]) AS Values;


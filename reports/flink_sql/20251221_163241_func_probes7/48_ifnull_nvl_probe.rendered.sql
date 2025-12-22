-- Flink: IFNULL / NVL function names (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  IFNULL(CAST(NULL AS STRING), 'fallback') AS IfNullValue,
  NVL(CAST(NULL AS STRING), 'fallback') AS NvlValue;


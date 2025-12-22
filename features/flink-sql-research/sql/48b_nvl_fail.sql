-- Flink: NVL function name (expected: NG)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  NVL(CAST(NULL AS STRING), 'fallback') AS NvlValue;


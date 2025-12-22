-- Flink: ARRAY access with 0-based index should fail (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  ARRAY[10, 20, 30][0] AS ArrIdx0;


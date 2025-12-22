-- Flink: string -> timestamp parsing (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CAST('2025-01-02 03:04:05' AS TIMESTAMP) AS CastTimestamp,
  TO_TIMESTAMP('2025-01-02 03:04:05') AS ToTimestamp_1Arg,
  TO_TIMESTAMP('2025-01-02 03:04:05', 'yyyy-MM-dd HH:mm:ss') AS ToTimestamp_2Arg,
  CURRENT_TIMESTAMP AS CurrentTimestamp;


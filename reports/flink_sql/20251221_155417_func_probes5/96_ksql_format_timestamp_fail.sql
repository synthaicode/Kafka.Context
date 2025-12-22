-- KSQL dialect function shape: FORMAT_TIMESTAMP(ts, 'w', 'UTC') (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT CAST(FORMAT_TIMESTAMP(TIMESTAMP '2025-01-02 03:04:05', 'w', 'UTC') AS INT) AS WeekNum;


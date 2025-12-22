-- KSQL dialect function shape: DATEADD('minute', 5, ts) (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT DATEADD('minute', 5, TIMESTAMP '2025-01-02 03:04:05') AS Added;


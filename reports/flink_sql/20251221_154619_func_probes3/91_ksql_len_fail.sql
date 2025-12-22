-- KSQL dialect function name: LEN (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT LEN('abc') AS Len;


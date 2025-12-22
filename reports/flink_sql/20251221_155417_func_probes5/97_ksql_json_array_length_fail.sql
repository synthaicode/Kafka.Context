-- KSQL dialect function: JSON_ARRAY_LENGTH (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT JSON_ARRAY_LENGTH('[1,2,3]') AS Len;


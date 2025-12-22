-- KSQL dialect function name: TIMESTAMPTOSTRING (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT TIMESTAMPTOSTRING(CAST(1700000000000 AS BIGINT), 'yyyy', 'UTC') AS YearStr;


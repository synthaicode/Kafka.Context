-- KSQL dialect function: JSON_EXTRACT_STRING (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT JSON_EXTRACT_STRING('{\"a\": {\"b\": \"x\"}}', '$.a.b') AS Val;


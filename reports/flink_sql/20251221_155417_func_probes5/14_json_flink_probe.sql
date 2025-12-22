-- Flink: JSON functions (may depend on runtime/version; treat as probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  JSON_VALUE('{\"a\":1,\"b\":{\"c\":\"x\"}}', '$.a') AS A,
  JSON_VALUE('{\"a\":1,\"b\":{\"c\":\"x\"}}', '$.b.c') AS BC;


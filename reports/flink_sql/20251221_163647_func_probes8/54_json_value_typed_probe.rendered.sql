-- Flink: JSON_VALUE typed return (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  JSON_VALUE('{\"a\":10,\"b\":true,\"c\":\"x\"}', '$.a') AS A_Str,
  CAST(JSON_VALUE('{\"a\":10,\"b\":true,\"c\":\"x\"}', '$.a') AS INT) AS A_Int,
  CAST(JSON_VALUE('{\"a\":10,\"b\":true,\"c\":\"x\"}', '$.b') AS BOOLEAN) AS B_Bool,
  JSON_VALUE('{\"a\":10,\"b\":true,\"c\":\"x\"}', '$.c') AS C_Str;


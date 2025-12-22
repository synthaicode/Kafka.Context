-- Deep dive: JSON object with array access (focus: why $.a[1] was NULL)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

-- Note: SQL string literals do NOT need escaping for double quotes.
SELECT
  '{"a":[10,20]}' AS RawJson,
  JSON_QUERY('{"a":[10,20]}', '$.a') AS A_Array,
  JSON_VALUE('{"a":[10,20]}', '$.a[0]') AS A0,
  JSON_VALUE('{"a":[10,20]}', '$.a[1]') AS A1,
  CAST(JSON_VALUE('{"a":[10,20]}', '$.a[1]') AS INT) AS A1_AsInt;


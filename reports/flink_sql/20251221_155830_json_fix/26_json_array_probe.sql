-- Probe: JSON array handling (Flink JSON_* functions)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  JSON_VALUE('[10,20,30]', '$[0]') AS FirstVal,
  JSON_VALUE('{\"a\":[10,20]}', '$.a[1]') AS SecondVal,
  JSON_QUERY('{\"a\":[10,20]}', '$.a') AS ArrJson;

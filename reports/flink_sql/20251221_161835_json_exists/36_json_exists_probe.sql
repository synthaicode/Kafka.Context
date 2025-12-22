-- Probe: JSON_EXISTS predicate

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  JSON_EXISTS('{"a":[10,20]}', '$.a[1]') AS ExistsA1,
  JSON_EXISTS('{"a":[10,20]}', '$.a[9]') AS ExistsA9;


-- Deep dive: JSON path variants (lax/strict + array on root/object)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  JSON_VALUE('[10,20,30]', '$[1]') AS Root1,
  JSON_VALUE('[10,20,30]', 'lax $[1]') AS LaxRoot1,
  JSON_VALUE('{"a":[10,20]}', 'lax $.a[1]') AS LaxA1,
  JSON_VALUE('{"a":[10,20]}', 'strict $.a[1]') AS StrictA1,
  JSON_QUERY('{"a":[10,20]}', '$.a[1]') AS QueryA1;


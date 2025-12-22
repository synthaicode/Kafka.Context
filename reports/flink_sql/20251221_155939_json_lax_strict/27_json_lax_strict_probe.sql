-- Probe: JSON path modes (lax/strict) for arrays and nested arrays

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  JSON_VALUE('{\"a\":[10,20]}', 'lax $.a[1]') AS LaxSecond,
  JSON_VALUE('{\"a\":[10,20]}', 'strict $.a[1]') AS StrictSecond;


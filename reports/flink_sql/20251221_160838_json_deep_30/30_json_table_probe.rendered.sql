-- Deep dive: JSON_TABLE availability (may not be supported depending on Flink/Calcite)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT *
FROM JSON_TABLE(
  '{"a":[10,20]}',
  '$.a[*]' COLUMNS (
    idx FOR ORDINALITY,
    v INT PATH '$'
  )
) AS T;


-- Flink: ARRAY/MAP literals and access (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  ARRAY[10, 20, 30] AS Arr,
  CARDINALITY(ARRAY[10, 20, 30]) AS ArrLen,
  ARRAY[10, 20, 30][1] AS ArrIdx1,
  ARRAY[10, 20, 30][0] AS ArrIdx0,
  MAP['a', 1, 'b', 2] AS M,
  MAP['a', 1, 'b', 2]['a'] AS MapGetA;


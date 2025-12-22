-- Flink: TRY_CAST (probe) - useful for "Linq may parse string -> number" shapes

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  TRY_CAST('123' AS INT) AS TryCastInt_Ok,
  TRY_CAST('x' AS INT) AS TryCastInt_Bad,
  TRY_CAST('2025-01-02 03:04:05' AS TIMESTAMP) AS TryCastTimestamp;


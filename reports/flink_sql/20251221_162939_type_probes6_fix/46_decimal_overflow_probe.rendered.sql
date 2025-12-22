-- Flink: DECIMAL overflow (expected: likely error)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CAST(123.45 AS DECIMAL(10, 2)) AS DecOk,
  TRY_CAST('12345678901234567890.12' AS DECIMAL(10, 2)) AS DecTooBig_TryCast;

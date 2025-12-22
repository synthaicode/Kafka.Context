-- Flink: DATE +/- INTERVAL shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  DATE '2025-01-02' + INTERVAL '1' DAY AS DatePlus1,
  DATE '2025-01-02' - INTERVAL '1' DAY AS DateMinus1,
  TIMESTAMP '2025-01-02 03:04:05' + INTERVAL '5' MINUTE AS TsPlus5Min;


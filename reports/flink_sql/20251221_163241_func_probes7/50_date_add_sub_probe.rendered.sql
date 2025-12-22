-- Flink: DATE_ADD/DATE_SUB shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  DATE_ADD(DATE '2025-01-02', 1) AS DateAdd1,
  DATE_SUB(DATE '2025-01-02', 1) AS DateSub1;


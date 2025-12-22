-- Flink: truncation shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  FLOOR(TIMESTAMP '2025-01-02 03:04:05' TO DAY) AS FloorToDay,
  FLOOR(TIMESTAMP '2025-01-02 03:04:05' TO HOUR) AS FloorToHour;


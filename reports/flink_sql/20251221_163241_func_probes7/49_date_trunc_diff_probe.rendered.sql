-- Flink: DATE_TRUNC / TIMESTAMPDIFF shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  DATE_TRUNC('DAY', TIMESTAMP '2025-01-02 03:04:05') AS TruncDay,
  DATE_TRUNC('HOUR', TIMESTAMP '2025-01-02 03:04:05') AS TruncHour,
  TIMESTAMPDIFF(MINUTE, TIMESTAMP '2025-01-02 03:00:00', TIMESTAMP '2025-01-02 03:04:05') AS DiffMin,
  TIMESTAMPDIFF(SECOND, TIMESTAMP '2025-01-02 03:00:00', TIMESTAMP '2025-01-02 03:04:05') AS DiffSec;


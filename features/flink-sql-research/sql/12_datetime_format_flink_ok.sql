-- Flink: formatting / add-interval shapes (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  DATE_FORMAT(TIMESTAMP '2025-01-02 03:04:05', 'yyyy-MM-dd HH:mm:ss') AS DateFormat,
  TIMESTAMPADD(MINUTE, 5, TIMESTAMP '2025-01-02 03:04:05') AS AddMinutes,
  TIMESTAMPADD(DAY, 1, TIMESTAMP '2025-01-02 03:04:05') AS AddDays;


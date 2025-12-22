-- Flink: datetime parts extraction (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  EXTRACT(YEAR FROM TIMESTAMP '2025-01-02 03:04:05') AS Year,
  EXTRACT(MONTH FROM TIMESTAMP '2025-01-02 03:04:05') AS Month,
  EXTRACT(DAY FROM TIMESTAMP '2025-01-02 03:04:05') AS Day,
  EXTRACT(HOUR FROM TIMESTAMP '2025-01-02 03:04:05') AS Hour,
  EXTRACT(MINUTE FROM TIMESTAMP '2025-01-02 03:04:05') AS Minute,
  EXTRACT(SECOND FROM TIMESTAMP '2025-01-02 03:04:05') AS Second;


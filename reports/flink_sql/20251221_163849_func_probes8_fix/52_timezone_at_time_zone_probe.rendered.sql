-- Flink: timezone conversion shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  TIMESTAMP '2025-01-02 03:04:05' AS BaseTs,
  TO_UTC_TIMESTAMP(TIMESTAMP '2025-01-02 03:04:05', 'Asia/Tokyo') AS ToUtcFromTokyo,
  FROM_UTC_TIMESTAMP(TIMESTAMP '2025-01-02 03:04:05', 'Asia/Tokyo') AS FromUtcToTokyo;

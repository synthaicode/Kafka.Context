-- Flink: table.local-time-zone effect on TIMESTAMP_LTZ rendering (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SET 'table.local-time-zone' = 'UTC';
SELECT
  'UTC' AS Zone,
  TO_TIMESTAMP_LTZ(1700000000000, 3) AS TsLtz;

SET 'table.local-time-zone' = 'Asia/Tokyo';
SELECT
  'Asia/Tokyo' AS Zone,
  TO_TIMESTAMP_LTZ(1700000000000, 3) AS TsLtz;


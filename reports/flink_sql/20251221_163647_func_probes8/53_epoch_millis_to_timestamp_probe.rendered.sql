-- Flink: epoch-millis -> timestamp/timestamp_ltz (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  TO_TIMESTAMP_LTZ(1700000000000, 3) AS TsLtzFromMillis,
  TO_TIMESTAMP_LTZ(1700000000, 0) AS TsLtzFromSeconds;


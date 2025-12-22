-- Flink: epoch-seconds -> timestamp function name (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  FROM_UNIXTIME(1700000000) AS FromUnixTime_Seconds;


-- Probe: WeekOfYear shapes (ksqlDB uses FORMAT_TIMESTAMP(...,'w',...))

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  EXTRACT(WEEK FROM TIMESTAMP '2025-01-02 03:04:05') AS WeekExtract,
  DATE_FORMAT(TIMESTAMP '2025-01-02 03:04:05', 'w') AS WeekFormat;


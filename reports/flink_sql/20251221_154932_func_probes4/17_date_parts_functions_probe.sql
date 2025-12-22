-- Probe: DayOfWeek/DayOfYear function names

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  DAYOFWEEK(TIMESTAMP '2025-01-02 03:04:05') AS Dow,
  DAYOFYEAR(TIMESTAMP '2025-01-02 03:04:05') AS Doy;


-- Probe: SPLIT / LPAD / RPAD (ksqlDB uses SPLIT/LPAD/RPAD)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  LPAD('7', 3, '0') AS Lpad1,
  RPAD('7', 3, '0') AS Rpad1,
  SPLIT('a,b,c', ',') AS SplitArr;


-- Flink: SUBSTRING index base (probe)
-- Note: C# Substring is 0-based; many SQL SUBSTRING are 1-based.

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  SUBSTRING('abcdef', 0, 3) AS Sub_0_3,
  SUBSTRING('abcdef', 1, 3) AS Sub_1_3,
  SUBSTRING('abcdef', 2, 3) AS Sub_2_3;


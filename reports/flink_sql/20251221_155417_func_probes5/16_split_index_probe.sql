-- Probe: Split element access (LINQ Split()[i] 相当)
-- Flink has SPLIT_INDEX in some versions; this checks availability.

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT
  SPLIT_INDEX('a,b,c', ',', 0) AS First0,
  SPLIT_INDEX('a,b,c', ',', 1) AS Second1;


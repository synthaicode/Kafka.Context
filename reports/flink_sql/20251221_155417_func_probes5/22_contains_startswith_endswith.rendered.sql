-- Probe: Contains / StartsWith / EndsWith (LINQ) equivalents in Flink

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  'abcdef' LIKE '%cd%' AS LikeContains,
  'abcdef' LIKE 'ab%' AS LikeStartsWith,
  'abcdef' LIKE '%ef' AS LikeEndsWith,
  POSITION('cd' IN 'abcdef') > 0 AS PositionContains,
  SUBSTRING('abcdef', 1, 2) = 'ab' AS SubstringStartsWith,
  SUBSTRING('abcdef', 5, 2) = 'ef' AS SubstringEndsWith;


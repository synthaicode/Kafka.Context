-- Flink: string functions that LINQ commonly needs (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  SUBSTRING('abcdef', 2, 3) AS Substring_2_3,
  TRIM('  a b  ') AS Trimmed,
  UPPER('Abc') AS Uppered,
  LOWER('AbC') AS Lowered,
  CHAR_LENGTH('abc') AS CharLength,
  POSITION('cd' IN 'abcdef') AS Pos,
  LOCATE('cd', 'abcdef') AS Loc,
  CONCAT('a', '-', 'b') AS Concatenated,
  REPLACE('a-b-c', '-', '_') AS Replaced,
  REGEXP_REPLACE('a--b', '-+', '-') AS RegexReplaced;

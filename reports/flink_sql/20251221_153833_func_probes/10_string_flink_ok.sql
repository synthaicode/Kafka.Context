-- Flink: string functions that LINQ commonly needs (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  SUBSTRING('abcdef', 2, 3) AS Substring_2_3,
  TRIM('  a b  ') AS Trim,
  UPPER('Abc') AS Upper,
  LOWER('AbC') AS Lower,
  CHAR_LENGTH('abc') AS CharLength,
  POSITION('cd' IN 'abcdef') AS Position,
  LOCATE('cd', 'abcdef') AS Locate,
  CONCAT('a', '-', 'b') AS Concat,
  REPLACE('a-b-c', '-', '_') AS Replace1,
  REGEXP_REPLACE('a--b', '-+', '-') AS RegexpReplace;


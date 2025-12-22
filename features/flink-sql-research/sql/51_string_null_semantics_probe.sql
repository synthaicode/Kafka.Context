-- Flink: string function NULL semantics (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CONCAT('a', CAST(NULL AS STRING), 'b') AS ConcatNull,
  REPLACE(CAST(NULL AS STRING), 'a', 'b') AS ReplaceNull,
  CHAR_LENGTH(CAST(NULL AS STRING)) AS LenNull,
  TRIM(CAST(NULL AS STRING)) AS TrimNull;


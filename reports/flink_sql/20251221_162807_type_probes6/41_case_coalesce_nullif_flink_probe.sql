-- Flink: CASE/COALESCE/NULLIF shapes (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CASE WHEN 1 = 1 THEN 'ok' ELSE 'ng' END AS CaseWhen,
  COALESCE(CAST(NULL AS STRING), 'fallback') AS Coalesce,
  NULLIF(1, 1) AS NullIf_Equal,
  NULLIF(1, 2) AS NullIf_NotEqual;


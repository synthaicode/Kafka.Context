-- Flink: CAST/DECIMAL (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CAST('123' AS INT) AS CastInt,
  CAST('1234567890123' AS BIGINT) AS CastBigInt,
  CAST('1.23' AS DECIMAL(10, 2)) AS CastDecimal,
  CAST(1.2 AS DOUBLE) AS CastDouble,
  CAST(TRUE AS BOOLEAN) AS CastBool,
  CAST(NULL AS STRING) AS CastNullString;


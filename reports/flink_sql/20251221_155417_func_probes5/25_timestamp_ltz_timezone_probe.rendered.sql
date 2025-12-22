-- Probe: TIMESTAMP_LTZ / epoch-millis / timezone related shapes

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  TO_TIMESTAMP_LTZ(CAST(1735689600000 AS BIGINT), 3) AS TsLtz,
  EXTRACT(YEAR FROM TO_TIMESTAMP_LTZ(CAST(1735689600000 AS BIGINT), 3)) AS YearPart,
  DATE_FORMAT(TO_TIMESTAMP_LTZ(CAST(1735689600000 AS BIGINT), 3), 'yyyy-MM-dd HH:mm:ss') AS TsFmt;


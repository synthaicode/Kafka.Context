-- Probe: DECIMAL operations/aggregations (precision/scale sensitivity)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE Prices (
  Symbol STRING,
  Px STRING
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '50',
  'fields.Symbol.kind' = 'random',
  'fields.Symbol.length' = '3',
  'fields.Px.kind' = 'random',
  'fields.Px.length' = '6'
);

SELECT
  Symbol,
  ROUND(AVG(CAST(Px AS DECIMAL(18, 4))), 2) AS AvgPx,
  SUM(CAST(Px AS DECIMAL(18, 4))) AS SumPx
FROM Prices
GROUP BY Symbol
LIMIT 20;


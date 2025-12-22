-- Probe: nested aggregate wrappers like ROUND(AVG(...), 1)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE Orders (
  CustomerId INT,
  Amount INT
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '50',
  'fields.CustomerId.kind' = 'random',
  'fields.CustomerId.min' = '1',
  'fields.CustomerId.max' = '5',
  'fields.Amount.kind' = 'random',
  'fields.Amount.min' = '10',
  'fields.Amount.max' = '200'
);

SELECT
  CustomerId,
  ROUND(AVG(Amount), 1) AS AvgRounded1,
  ROUND(SUM(Amount) / CAST(COUNT(*) AS DOUBLE), 1) AS AvgEmulated1
FROM Orders
GROUP BY CustomerId
LIMIT 20;


-- GROUP BY / Aggregate (bounded datagen -> SELECT result)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'table';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE Orders (
  OrderId INT,
  CustomerId INT,
  Amount INT
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '30',
  'fields.OrderId.kind' = 'sequence',
  'fields.OrderId.start' = '1',
  'fields.OrderId.end' = '30',
  'fields.CustomerId.kind' = 'random',
  'fields.CustomerId.min' = '1',
  'fields.CustomerId.max' = '5',
  'fields.Amount.kind' = 'random',
  'fields.Amount.min' = '10',
  'fields.Amount.max' = '200'
);

SELECT
  CustomerId,
  COUNT(*) AS Cnt,
  SUM(Amount) AS SumAmount
FROM Orders
GROUP BY CustomerId
LIMIT 20;

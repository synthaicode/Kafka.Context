-- JOIN shape (bounded datagen -> SELECT result)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'table';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE Customers (
  CustomerId INT,
  Name STRING
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '3',
  'fields.CustomerId.kind' = 'sequence',
  'fields.CustomerId.start' = '1',
  'fields.CustomerId.end' = '3',
  'fields.Name.kind' = 'random',
  'fields.Name.length' = '8'
);

CREATE TABLE Orders (
  OrderId INT,
  CustomerId INT,
  Amount INT
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '10',
  'fields.OrderId.kind' = 'sequence',
  'fields.OrderId.start' = '1',
  'fields.OrderId.end' = '10',
  'fields.CustomerId.kind' = 'random',
  'fields.CustomerId.min' = '1',
  'fields.CustomerId.max' = '3',
  'fields.Amount.kind' = 'random',
  'fields.Amount.min' = '10',
  'fields.Amount.max' = '200'
);

SELECT o.OrderId, o.CustomerId, c.Name, o.Amount
FROM Orders AS o
JOIN Customers AS c
ON o.CustomerId = c.CustomerId
LIMIT 20;

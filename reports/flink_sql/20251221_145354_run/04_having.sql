-- HAVING (bounded datagen -> print)

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

CREATE TABLE OutPrint (
  CustomerId INT,
  Cnt BIGINT
) WITH (
  'connector' = 'print'
);

INSERT INTO OutPrint
SELECT CustomerId, COUNT(*) AS Cnt
FROM Orders
GROUP BY CustomerId
HAVING COUNT(*) >= 5;


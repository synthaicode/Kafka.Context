-- Basic SELECT/WHERE/SELECT projection (bounded datagen -> print)

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

CREATE TABLE OutPrint (
  OrderId INT,
  CustomerId INT,
  Amount INT
) WITH (
  'connector' = 'print'
);

INSERT INTO OutPrint
SELECT OrderId, CustomerId, Amount
FROM Orders
WHERE Amount >= 100;


-- GROUP BY / Aggregate (bounded datagen -> filesystem)

SET 'execution.runtime-mode' = 'batch';

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

CREATE TABLE OutFile (
  CustomerId INT,
  Cnt BIGINT,
  SumAmount BIGINT
) WITH (
  'connector' = 'filesystem',
  'path' = '/out/20251221_151550_suite_volume/03_groupby',
  'format' = 'csv'
);

INSERT INTO OutFile
SELECT
  CustomerId,
  COUNT(*) AS Cnt,
  SUM(Amount) AS SumAmount
FROM Orders
GROUP BY CustomerId;

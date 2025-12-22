-- HAVING (bounded datagen -> filesystem)

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
  Cnt BIGINT
) WITH (
  'connector' = 'filesystem',
  'path' = '/workspace/reports/flink_sql/20251221_151329_suite_5_root/04_having',
  'format' = 'csv'
);

INSERT INTO OutFile
SELECT CustomerId, COUNT(*) AS Cnt
FROM Orders
GROUP BY CustomerId
HAVING COUNT(*) >= 5;

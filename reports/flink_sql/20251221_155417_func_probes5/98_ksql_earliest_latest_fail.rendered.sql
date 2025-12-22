-- KSQL dialect aggregates: EARLIEST_BY_OFFSET / LATEST_BY_OFFSET (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '20';

CREATE TABLE Orders (
  CustomerId INT,
  Amount INT
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '30',
  'fields.CustomerId.kind' = 'random',
  'fields.CustomerId.min' = '1',
  'fields.CustomerId.max' = '3',
  'fields.Amount.kind' = 'random',
  'fields.Amount.min' = '10',
  'fields.Amount.max' = '200'
);

SELECT
  CustomerId,
  EARLIEST_BY_OFFSET(Amount) AS FirstAmount,
  LATEST_BY_OFFSET(Amount) AS LastAmount
FROM Orders
GROUP BY CustomerId;


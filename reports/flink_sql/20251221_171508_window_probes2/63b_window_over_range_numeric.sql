-- OVER window (RANGE on numeric order key) (probe)
-- Workaround for "RANGE BETWEEN INTERVAL ..." temporal arithmetic.

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE OrdersOverRangeNum (
  OrderId INT,
  CustomerId INT,
  Amount INT,
  TsMs BIGINT,
  Ts AS TO_TIMESTAMP_LTZ(TsMs, 3),
  WATERMARK FOR Ts AS Ts - INTERVAL '1' SECOND
) WITH (
  'connector' = 'datagen',
  'number-of-rows' = '10',
  'fields.OrderId.kind' = 'sequence',
  'fields.OrderId.start' = '1',
  'fields.OrderId.end' = '10',
  'fields.CustomerId.kind' = 'sequence',
  'fields.CustomerId.start' = '1',
  'fields.CustomerId.end' = '1',
  'fields.Amount.kind' = 'sequence',
  'fields.Amount.start' = '10',
  'fields.Amount.end' = '100',
  'fields.TsMs.kind' = 'sequence',
  'fields.TsMs.start' = '1735689600000',
  'fields.TsMs.end' = '1735689609000'
);

SELECT
  OrderId,
  TsMs,
  Amount,
  SUM(Amount) OVER (
    PARTITION BY CustomerId
    ORDER BY TsMs
    RANGE BETWEEN 2000 PRECEDING AND CURRENT ROW
  ) AS SumLast2s
FROM OrdersOverRangeNum
ORDER BY OrderId
LIMIT 50;


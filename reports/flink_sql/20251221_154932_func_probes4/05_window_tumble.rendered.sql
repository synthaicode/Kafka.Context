-- Window aggregation (TUMBLE) (bounded datagen -> SELECT result)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE Orders (
  OrderId INT,
  CustomerId INT,
  Amount INT,
  TsMs BIGINT,
  Ts AS TO_TIMESTAMP_LTZ(TsMs, 3),
  WATERMARK FOR Ts AS Ts - INTERVAL '1' SECOND
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
  'fields.Amount.max' = '200',
  'fields.TsMs.kind' = 'sequence',
  'fields.TsMs.start' = '1735689600000',
  'fields.TsMs.end' = '1735689620000'
);

SELECT
  window_start,
  window_end,
  CustomerId,
  COUNT(*) AS Cnt
FROM TABLE(
  TUMBLE(TABLE Orders, DESCRIPTOR(Ts), INTERVAL '5' SECOND)
)
GROUP BY window_start, window_end, CustomerId
LIMIT 50;

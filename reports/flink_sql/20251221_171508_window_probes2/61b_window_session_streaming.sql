-- Window aggregation (SESSION) in streaming runtime (probe)
-- Note: session window is "unaligned"; batch mode does not support it yet.

SET 'execution.runtime-mode' = 'streaming';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE OrdersSessionStreaming (
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
  'fields.CustomerId.max' = '2',
  'fields.Amount.kind' = 'random',
  'fields.Amount.min' = '10',
  'fields.Amount.max' = '200',
  'fields.TsMs.kind' = 'sequence',
  'fields.TsMs.start' = '1735689600000',
  'fields.TsMs.end' = '1735689700000'
);

SELECT
  window_start,
  window_end,
  CustomerId,
  COUNT(*) AS Cnt
FROM TABLE(
  SESSION(TABLE OrdersSessionStreaming, DESCRIPTOR(Ts), INTERVAL '10' SECOND)
)
GROUP BY window_start, window_end, CustomerId
ORDER BY window_start, CustomerId
LIMIT 50;


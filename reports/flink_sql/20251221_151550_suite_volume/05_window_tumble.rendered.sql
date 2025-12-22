-- Window aggregation (TUMBLE) (bounded datagen -> filesystem)

SET 'execution.runtime-mode' = 'batch';

CREATE TABLE Orders (
  OrderId INT,
  CustomerId INT,
  Amount INT,
  Ts TIMESTAMP(3),
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
  'fields.Ts.kind' = 'sequence',
  'fields.Ts.start' = '2025-01-01T00:00:00.000',
  'fields.Ts.end' = '2025-01-01T00:00:20.000'
);

CREATE TABLE OutFile (
  WindowStart TIMESTAMP(3),
  WindowEnd TIMESTAMP(3),
  CustomerId INT,
  Cnt BIGINT
) WITH (
  'connector' = 'filesystem',
  'path' = '/out/20251221_151550_suite_volume/05_window_tumble',
  'format' = 'csv'
);

INSERT INTO OutFile
SELECT
  window_start,
  window_end,
  CustomerId,
  COUNT(*) AS Cnt
FROM TABLE(
  TUMBLE(TABLE Orders, DESCRIPTOR(Ts), INTERVAL '5' SECOND)
)
GROUP BY window_start, window_end, CustomerId;

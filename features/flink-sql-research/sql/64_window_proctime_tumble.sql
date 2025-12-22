-- Window aggregation (TUMBLE on PROCTIME) (probe)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

CREATE TABLE OrdersProcTime (
  OrderId INT,
  CustomerId INT,
  Amount INT,
  Pt AS PROCTIME()
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

SELECT
  window_start,
  window_end,
  CustomerId,
  COUNT(*) AS Cnt
FROM TABLE(
  TUMBLE(TABLE OrdersProcTime, DESCRIPTOR(Pt), INTERVAL '5' SECOND)
)
GROUP BY window_start, window_end, CustomerId
ORDER BY window_start, CustomerId
LIMIT 50;


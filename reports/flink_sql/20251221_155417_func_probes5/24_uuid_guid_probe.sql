-- Probe: UUID/GUID handling (LINQ Guid) in Flink
-- Note: some runtimes do not expose UUID type/functions; treat this as compatibility probe.

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  CAST('550e8400-e29b-41d4-a716-446655440000' AS STRING) AS GuidString,
  UUID() AS UuidFn;


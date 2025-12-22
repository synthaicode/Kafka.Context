-- Probe: JSON object keys (candidates: JSON_KEYS / JSON_OBJECT_KEYS)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT JSON_KEYS('{"a":1,"b":2}') AS Keys;


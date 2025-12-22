-- Probe: JSON length / array length helpers (candidate: JSON_LENGTH)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT JSON_LENGTH('[1,2,3]') AS Len;


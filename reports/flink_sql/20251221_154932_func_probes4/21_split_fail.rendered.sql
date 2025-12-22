-- Probe: SPLIT(string, delimiter) (ksqlDB uses SPLIT; expected: FAIL in this Flink image)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT SPLIT('a,b,c', ',') AS SplitArr;


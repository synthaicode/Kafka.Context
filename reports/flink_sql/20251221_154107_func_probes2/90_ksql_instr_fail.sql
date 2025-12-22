-- KSQL dialect function name: INSTR (expected: FAIL in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT INSTR('abcdef', 'cd') AS Instr;


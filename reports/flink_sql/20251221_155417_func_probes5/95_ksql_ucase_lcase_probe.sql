-- KSQL dialect function names: UCASE/LCASE (probe; may or may not exist in Flink)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';

SELECT UCASE('Abc') AS U, LCASE('Abc') AS L;


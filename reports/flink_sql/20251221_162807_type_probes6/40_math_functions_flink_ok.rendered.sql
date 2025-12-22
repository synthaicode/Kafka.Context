-- Flink: math functions that LINQ commonly needs (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  ROUND(1.2345, 2) AS Round_2,
  FLOOR(1.9) AS Floor_1_9,
  CEIL(1.1) AS Ceil_1_1,
  ABS(-3) AS Abs_3,
  POWER(2, 3) AS Pow_2_3,
  SQRT(9) AS Sqrt_9,
  LOG(10) AS Log_10,
  EXP(1) AS Exp_1;


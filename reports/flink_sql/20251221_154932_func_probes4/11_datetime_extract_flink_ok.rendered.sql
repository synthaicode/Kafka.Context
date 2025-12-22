-- Flink: datetime parts extraction (expected: OK)

SET 'execution.runtime-mode' = 'batch';
SET 'sql-client.execution.result-mode' = 'tableau';
SET 'sql-client.execution.max-table-result.rows' = '50';

SELECT
  EXTRACT(YEAR FROM TIMESTAMP '2025-01-02 03:04:05') AS YearPart,
  EXTRACT(MONTH FROM TIMESTAMP '2025-01-02 03:04:05') AS MonthPart,
  EXTRACT(DAY FROM TIMESTAMP '2025-01-02 03:04:05') AS DayPart,
  EXTRACT(HOUR FROM TIMESTAMP '2025-01-02 03:04:05') AS HourPart,
  EXTRACT(MINUTE FROM TIMESTAMP '2025-01-02 03:04:05') AS MinutePart,
  EXTRACT(SECOND FROM TIMESTAMP '2025-01-02 03:04:05') AS SecondPart;

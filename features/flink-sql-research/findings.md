# Flink SQL: LINQ/ksqlDB 由来の「使える/使えない」初期観測

このメモは `C:\dev\Ksql.Linq.wiki` の ksqlDB 方言前提の関数（例: `INSTR`, `LEN`, `TIMESTAMPTOSTRING`, `DATEADD`）が **Flink SQL ではそのまま通らない**点を、docker 実行ログで履歴化したもの。

## 実行履歴（例）

- `reports/flink_sql/20251221_155417_func_probes5/report.md`
- `reports/flink_sql/20251221_155918_regex_extract_only/report.md`
- `reports/flink_sql/20251221_155929_regex_predicate/report.md`
- `reports/flink_sql/20251221_155830_json_fix/report.md`
- `reports/flink_sql/20251221_160816_json_deep_28/report.md`
- `reports/flink_sql/20251221_160827_json_deep_29/report.md`
- `reports/flink_sql/20251221_160838_json_deep_30/report.md`
- `reports/flink_sql/20251221_161758_json_len/report.md`
- `reports/flink_sql/20251221_161807_json_array_len/report.md`
- `reports/flink_sql/20251221_161817_json_keys/report.md`
- `reports/flink_sql/20251221_161826_json_object_keys/report.md`
- `reports/flink_sql/20251221_161835_json_exists/report.md`

## 代表例（ksqlDB → Flink）

| LINQ想定 | ksqlDB 側の形（wiki） | Flink 側の形 | Flinkでの結論 |
|---|---|---|---|
| `IndexOf` | `INSTR(s, sub)` | `POSITION(sub IN s)` / `LOCATE(sub, s)` | **Flinkでも `INSTR` はOK（ただし `POSITION/LOCATE` の方が移植性が高い）** |
| `Length` | `LEN(s)` | `CHAR_LENGTH(s)` | **ksql形はNG / Flink形はOK** |
| `Split` | `SPLIT(s, delim)` | `SPLIT_INDEX(s, delim, i)` | **`SPLIT` はNG / `SPLIT_INDEX` はOK** |
| `PadLeft/PadRight` | `LPAD/RPAD` | `LPAD/RPAD` | **OK** |
| `Contains/StartsWith/EndsWith` | `INSTR/LIKE` など | `LIKE '%x%'` / `LIKE 'x%'` / `LIKE '%x'` | **OK（最小は LIKE）** |
| Regex（抽出/置換） | `REGEXP_*` | `REGEXP_EXTRACT` / `REGEXP_REPLACE` | **OK** |
| Regex（判定） | （runtime依存） | `SIMILAR TO` | **OK（`REGEXP_LIKE`/`RLIKE` はNG）** |
| 日付部品（epoch-millis） | `TIMESTAMPTOSTRING(CAST(x AS BIGINT), 'yyyy', 'UTC')` | `EXTRACT(YEAR FROM CAST(x AS TIMESTAMP))` 等（要: 方言設計） | **ksql形はNG** |
| 日付加算 | `DATEADD('minute', 5, ts)` | `TIMESTAMPADD(MINUTE, 5, ts)` / `ts + INTERVAL '5' MINUTE` | **ksql形はNG / Flink形はOK** |
| JSON抽出 | `JSON_EXTRACT_STRING(...)` | `JSON_VALUE(...)` | **ksql形はNG / Flink形はOK(要検証)** |
| 大文字小文字 | `UCASE/LCASE` | `UPPER/LOWER` | **probe**（Flink側の有無を確認） |
| 週番号 | `FORMAT_TIMESTAMP(dt,'w','UTC')` | `EXTRACT(WEEK FROM dt)` / `DATE_FORMAT(dt,'w')` | **ksql形はNG / Flink形はOK** |
| Guid/UUID | （文字列扱いが多い） | `UUID()` / `STRING` | **`UUID()` はOK、Guidは当面STRINGが安全** |
| JSON配列（パス） | `JSON_ARRAY_LENGTH` など | `JSON_VALUE('[..]','$[0]')` / `JSON_VALUE('{"a":[..]}','$.a[i]')` | **OK（ただし JSON 文字列の `\"` エスケープを入れると NULL になり得る → 生の `{"a":[..]}` を使う）** |
| JSON_QUERY の配列要素 |  | `JSON_QUERY('{"a":[10,20]}','$.a[1]')` | **この環境では NULL（`JSON_VALUE` はOK）** |
| JSONの存在判定 |  | `JSON_EXISTS(json, path)` | **OK** |
| JSON長さ/キー | `JSON_ARRAY_LENGTH` / `JSON_KEYS` | （候補なし） | **`JSON_LENGTH/JSON_ARRAY_LENGTH/JSON_KEYS/JSON_OBJECT_KEYS` はNG（方針B: Flink方言では非対応として fail-fast）** |
| 条件/NULL | `CASE`/`COALESCE`/`NULLIF` | 同左 | **OK（注意: `AS Coalesce` のような予約語エイリアスはパースエラーになり得る）** |
| パース（string→timestamp） | （runtime依存） | `CAST('..' AS TIMESTAMP)` / `TO_TIMESTAMP('..')` | **OK** |
| epoch→timestamp | `TIMESTAMPTOSTRING` 等 | `FROM_UNIXTIME(epochSeconds)` | **OK** |
| epoch→timestamp_ltz | （runtime依存） | `TO_TIMESTAMP_LTZ(epoch, precision)` | **OK（`table.local-time-zone` により表示/解釈が変わる）** |
| TimeZone変換 | （runtime依存） | `AT TIME ZONE` / `TO_UTC_TIMESTAMP` | **NG（代替: epoch + `TO_TIMESTAMP_LTZ` + `table.local-time-zone`）** |
| 安全キャスト | （なし） | `TRY_CAST(s AS INT/DECIMAL/...)` | **OK（失敗は NULL）** |
| Substringの開始index | 0-based（C#） | 1-based（SQL） | **Flinkでは `SUBSTRING(s, 0, n)` が `SUBSTRING(s, 1, n)` 相当（ただし変換側で 0→1 に補正推奨）** |
| ARRAY/MAP | （C#で配列/辞書） | `ARRAY[...]` / `MAP[...]` / `CARDINALITY` / `arr[i]` | **OK（配列の index は 1-based。`arr[0]` はエラー）** |
| ARRAY helpers | （C#で配列） | `ARRAY_CONTAINS` / `ARRAY_DISTINCT` / `ARRAY_JOIN` | **OK** |
| IFNULL | `IFNULL(x, y)` | `IFNULL(x, y)` | **OK** |
| NVL | `NVL(x, y)` | （なし） | **NG（関数シグネチャなし）** |
| MAP keys/values | （C#で辞書） | `MAP_KEYS` / `MAP_VALUES` | **OK（注意: `AS Values` は予約語でパースエラーになり得る）** |
| 日付切り捨て | （runtime依存） | `FLOOR(ts TO DAY/HOUR)` | **OK（`DATE_TRUNC('DAY', ts)` はNG）** |
| 時刻差分 | （runtime依存） | `TIMESTAMPDIFF(unit, t1, t2)` | **OK** |
| Date加減算 | `DATEADD` 等 | `date + INTERVAL 'n' DAY` / `ts + INTERVAL 'n' MINUTE` | **OK（`DATE_ADD(date, 1)` はNG）** |
| NULL伝播 | （SQL依存） | `CONCAT/REPLACE/CHAR_LENGTH/TRIM` | **NULL は NULL として伝播** |
| LIKE escape | （必要時） | `LIKE 'a^_b' ESCAPE '^'` | **OK（`ESCAPE '\\\\'` はNGだった）** |
| Window（TUMBLE） |  | `TABLE(TUMBLE(...))` | **OK（batch）** |
| Window（HOP） |  | `TABLE(HOP(...))` | **OK（batch）** |
| Window（SESSION） |  | `TABLE(SESSION(...))` | **batch は NG（未対応）。streaming は OK（changelogで +I/-D が出る）** |
| Window（PROCTIME） |  | `DESCRIPTOR(PROCTIME())` | **NG（Processing time Window TVF は未対応）** |
| OVER（ROWS） |  | `SUM(x) OVER (... ROWS ...)` | **実行は通るが、この環境では結果が 1 行しか出ないため要再検証** |
| OVER（RANGE interval） |  | `RANGE BETWEEN INTERVAL ...` | **NG（Unsupported temporal arithmetic）** |
| OVER（RANGE numeric） |  | `ORDER BY TsMs RANGE BETWEEN 2000 PRECEDING ...` | **実行は通るが、この環境では結果が 1 行しか出ないため要再検証** |

## 追加の観点（次に増やす）

- `SPLIT` / `LPAD` / `RPAD` / `FORMAT_TIMESTAMP` などの ksqlDB 固有関数の有無
- `DECIMAL` の演算・丸め・キャスト境界（精度/scale）
- `DATE`/`TIME`/`TIMESTAMP_LTZ` など、型の差で落ちるパターン
- `Guid/UUID` 相当の取り扱い（文字列/バイナリ）

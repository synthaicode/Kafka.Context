# diff: streaming flink json policy b (2025-12-21)

## Summary

- Flink 方言における JSON の「長さ/キー取得」系を **方針B（非対応・fail-fast）** として明文化する。

## Decision

- Flink SQL で `JSON_LENGTH` / `JSON_ARRAY_LENGTH` / `JSON_KEYS` / `JSON_OBJECT_KEYS` が利用できないため、Flink 方言ではこれらをサポート対象外とする。
- JSON パス参照は `JSON_VALUE` / `JSON_EXISTS` の範囲に限定する。

## Evidence

- `docs/diff_log/diff_flink_sql_json_keys_length_20251221.md`
- `reports/flink_sql/20251221_161758_json_len/report.md`
- `reports/flink_sql/20251221_161807_json_array_len/report.md`
- `reports/flink_sql/20251221_161817_json_keys/report.md`
- `reports/flink_sql/20251221_161826_json_object_keys/report.md`
- `reports/flink_sql/20251221_161835_json_exists/report.md`

## Spec Updates

- `docs/kafka-context-streaming-spec.md` に「Flink 方言の制約（JSON：方針B）」を追記。
- `features/flink-sql-research/findings.md` に方針Bを注記。


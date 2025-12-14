# diff_nuget_readme_nongoals_key_contract_preview_20251214

## 目的
NuGet README で期待値を固定し、利用者の自己解決率を上げる（「やらないこと」「key と SR の扱い」「事前レビュー価値」）。

## 変更概要
- Non-Goals を追記（Produce/Consume only の意図を強化）。
- key と SR の契約を 1 行で明記（単一 key は SR 未登録、複合 key のみ契約扱い）。
- `PreviewSchemaRegistryAvro()` の出力例を 1 行増やし、KeySubject/ValueSubject の存在が分かるようにした。


# Kafka.Context Schema Scaffold 機能要件

## 概要

Schema Registry (SR) に登録された Avro スキーマから C# の型定義（class / record）を生成する機能。
生成した型（POCO）を Kafka.Context に組み込み、従来どおり **POCO → Schema** を正として `ProvisionAsync()`（register/verify）を実行する運用を前提とする。

補足: fingerprint ベースの整合性検証（fail-fast）は **ランタイムではなく CLI/CI** で実施する。

## 機能範囲

### scaffold コマンド

SR から Avro スキーマを取得し、C# コードを生成する。

```bash
dotnet kafka-context schema scaffold --subject <subject-name> [options]
```

#### オプション

| オプション | 説明 | デフォルト |
|-----------|------|-----------|
| `--subject` | SR の subject 名（必須） | - |
| `--output` | 出力ディレクトリ | `./` |
| `--style` | `record` または `class` | `record` |
| `--force` | 既存ファイルを上書き | `false` |
| `--dry-run` | 生成内容をプレビュー（ファイル出力しない） | `false` |
| `--sr-url` | Schema Registry URL | appsettings.json から取得 |

#### 出力例

```csharp
using Kafka.Context.Attributes;

namespace Kafka.Context.Generated;

[KafkaTopic("orders")]
[SchemaSubject("orders-value")]
[SchemaFingerprint("a3b8f2c1d4e5...")]
public record Order(
    int Id,
    string CustomerId,
    decimal Amount,
    string? ShippingAddress
);
```

### verify コマンド（CI 推奨）

SR の schema と、ローカルの POCO から生成される schema が一致するか（fingerprint）を検証する。

```bash
dotnet kafka-context schema verify --subject <subject-name> [--type <assembly-qualified-type> | --fingerprint <hex>] [options]
```

#### 目的（fail-fast の位置づけ）

- デプロイ前（CI/開発端末）に不一致を検出して止める。
- ランタイム（本体）に「外部 schema を正として照合する」責務を持ち込まない。

## 型変換ルール

| Avro 型 | C# 型 |
|--------|-------|
| `null` union | nullable (`?`) |
| `boolean` | `bool` |
| `int` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double` | `double` |
| `bytes` | `byte[]` |
| `string` | `string` |
| `array` | `IReadOnlyList<T>` |
| `map` | `IReadOnlyDictionary<string, T>` |
| `enum` | `enum`（同名で生成） |
| `record`（ネスト） | nested record / class |
| `logical:decimal` | `decimal` |
| `logical:uuid` | `Guid` |
| `logical:date` | `DateOnly` |
| `logical:timestamp-millis` | `DateTime` |
| `logical:timestamp-micros` | `DateTime` |

### 型変換オプション

```bash
--dates-as-datetime    # DateOnly → DateTime
--arrays-as-list       # IReadOnlyList<T> → List<T>
```

## 既存ファイルの扱い

| 状況 | デフォルト動作 |
|-----|---------------|
| ファイルが存在しない | 生成 |
| ファイルが存在する | スキップ + 警告表示 |
| `--force` 指定時 | 上書き |

## 責務外（Non-Goals）

以下は本機能の責務外とする：

- 差分追跡・バージョン履歴管理
- スキーマ互換性モード（BACKWARD / FORWARD / FULL）の判定・選択
- 自動同期・自動マイグレーション
- 既存コードとのマージ
- ランタイム（`ProvisionAsync()`）での fingerprint 照合（外部 schema を正とする照合）

これらは開発者の判断に委ねる。

## Fingerprint による整合性検証

### Fingerprint の定義

Avro スキーマの Canonical Form から算出した SHA-256 ハッシュ値。

環境依存の要素（Schema ID、Version 番号、登録日時）を除外し、構造のみを比較可能にする。

### 検証対象

| 検証する（環境非依存） | 検証しない（環境依存） |
|----------------------|---------------------|
| フィールド名 | Schema ID |
| フィールド型 | Version 番号 |
| nullable / required | 登録日時 |
| デフォルト値 | |
| enum の symbols | |

### CLI/CI（verify）での検証

`verify` コマンドで fingerprint 照合を行う。
ランタイムは従来どおり `ProvisionAsync()` で register/verify を行い、整合性はデプロイ前に担保する。

```bash
# CI/開発端末で実行: SR の最新 schema と POCO->Schema の一致を検証して止める
dotnet kafka-context schema verify --subject orders-value --type Kafka.Context.Generated.Order, MyApp

# もしくは、型をロードせず fingerprint を直接指定する
dotnet kafka-context schema verify --subject orders-value --fingerprint 0123abcd...
```

#### 不一致時の出力例（verify）

```
Schema contract mismatch for 'orders-value':
  Expected fingerprint: a3b8f2c1...
  Actual fingerprint:   d7e9f0a2...

Diff:
  + field 'TrackingNumber' (string, nullable)
  - field 'ShippingAddress'
  ~ field 'Amount': int → long
```

## 運用フロー

```
┌─────────┐
│  開発   │  scaffold 実行 → C# コード生成（必要なら fingerprint も埋め込み）
└────┬────┘
     │ コミット
     ▼
┌─────────┐
│   CI    │  build/test + schema verify（fingerprint）
└────┬────┘
     │ デプロイ
     ▼
┌─────────┐
│  検証   │  起動 → ProvisionAsync（register/verify）
└────┬────┘
     │ デプロイ
     ▼
┌─────────┐
│  本番   │  起動 → ProvisionAsync（register/verify）
└─────────┘
```

### Fail-Fast の原則

デプロイ前（CI/開発端末）に SR との整合性を検証し、不一致があれば即座に失敗させる。これにより：

- 実行時エラーを未然に防止
- デプロイ直後に問題を検出
- 本番での予期しない障害を回避

## 実装方針

### 新規追加

1. `dotnet kafka-context schema scaffold` コマンド
2. `dotnet kafka-context schema verify` コマンド
3. `SchemaSubjectAttribute` 属性
4. `SchemaFingerprintAttribute` 属性
5. Avro → C# 型変換ロジック

### 既存拡張（不要）

- `ProvisionAsync()` の責務は現状維持（register/verify）。fingerprint 照合は CLI/CI に寄せる。

## 将来の拡張可能性

以下は現時点では実装しないが、将来の拡張として検討可能：

- `--output-json` オプション（AI 向け構造化出力）
- 複数 subject の一括 scaffold
- schema diff の構造化出力（互換性判定の補助）

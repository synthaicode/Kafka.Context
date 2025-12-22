# Kafka.Context 全体計画

## エグゼクティブサマリー

Kafka.Context は「ETL の隙間を埋める .NET Kafka 統合層」として、アプリケーションコードを挟める柔軟性を提供する。EF Core 開発者に馴染みのあるインターフェースで、Kafka エコシステム全体へのブリッジとなる。

---

## ポジショニング

### 市場における位置づけ

```
┌─────────────────────────────────────────────────┐
│              データ処理の選択肢                 │
├─────────────────────────────────────────────────┤
│  大量・定型        Flink Connector / ETL        │
│       ↑                                         │
│       │                                         │
│       │            Kafka.Context                │
│       │         （小回りが利く統合層）          │
│       ↓                                         │
│  少量・柔軟        アプリ内処理                 │
└─────────────────────────────────────────────────┘
```

### ETL との差別化

**ETL の得意領域**
- 大量データの高速転送
- 定型的な変換
- スケジュールベースの処理

**ETL の苦手領域（Kafka.Context が埋める）**
- 条件分岐が多いビジネスロジック
- 外部 API 呼び出しを含む処理
- 既存 .NET ライブラリとの連携
- イベント単位の細かい判断
- エラー時の個別ハンドリング
- 人間の承認を挟むワークフロー

### KafkaFlow との比較

| 観点 | Kafka.Context | KafkaFlow |
|------|--------------|-----------|
| 設計思想 | Contract-First / Fail-Fast | 運用重視 / 柔軟性 |
| IF スタイル | EF-like | Handler / Middleware |
| Dashboard | なし（Datadog 等に委譲） | あり |
| Middleware | なし（シンプル） | 豊富 |
| SR 統合 | 一級市民（ProvisionAsync） | プラグイン |
| Schema 管理 CLI | あり | なし |
| Flink 連携 | 設計済み | なし |

**方向性の違い**
- KafkaFlow: 「運用で困らない」を重視
- Kafka.Context: 「契約で壊れない」を重視

### 薄いラッパーの思想（Dapper 方式）

```
Dapper:         SQL + 型マッピング = 終わり
Kafka.Context:  Produce/Consume + 契約検証 = 終わり
```

**薄さの価値**
- 依存が少ない → 脆弱性対応が少ない
- 機能が少ない → バグが少ない
- コードが少ない → 理解しやすい
- 判断が少ない → 破壊的変更が少ない

**監視は外部ツールへ委譲**
- Dashboard を自前で持たない
- OpenTelemetry でメトリクス出力
- Datadog / Prometheus / Grafana で可視化

---

## アーキテクチャ

### ブリッジとしての役割

```
                    ┌─────────────────────────────────────┐
                    │         .NET Application            │
                    └──────────────┬──────────────────────┘
                                   │
                    ┌──────────────▼──────────────────────┐
                    │          Kafka.Context              │
                    │   ┌─────────────────────────────┐   │
                    │   │  EF-like IF (LINQ, DbSet)   │   │
                    │   └─────────────────────────────┘   │
                    └──┬──────────┬──────────┬───────────┘
                       │          │          │
          ┌────────────▼──┐  ┌────▼────┐  ┌──▼────────────┐
          │ Schema Registry│  │  Kafka  │  │   Streaming   │
          │   (Contract)   │  │ Topics  │  │    Engine     │
          └────────────────┘  └─────────┘  └───┬───────┬───┘
                                               │       │
                                           ┌───▼───┐ ┌─▼────┐
                                           │ Flink │ │ksqlDB│
                                           └───────┘ └──────┘
```

### .NET を挟むメリット

**Flink Connector 直接**
```
Source → Flink → Sink
        (SQL のみ)
```

**Kafka.Context 経由**
```
Source → .NET App → Kafka → Flink → Kafka → .NET App → Sink
              ↑                                   ↑
         任意のコード                         任意のコード
```

**挟めるもの**
```csharp
await ctx.Orders.ForEachAsync(async order =>
{
    // ここに何でも書ける
    var customer = await customerApi.GetAsync(order.CustomerId);
    var risk = await mlService.PredictRisk(order);
    var validated = businessRules.Validate(order);
    
    if (validated.IsValid)
    {
        await dbContext.ProcessedOrders.AddAsync(validated);
        await ctx.ProcessedOrders.AddAsync(validated);
    }
});
```

---

## パッケージ構成

### 現行パッケージ

```
Kafka.Context                     ← メインパッケージ
Kafka.Context.Abstractions        ← インターフェース
Kafka.Context.Application         ← ユースケース
Kafka.Context.Messaging           ← メッセージング
Kafka.Context.Infrastructure      ← インフラ層
Kafka.Context.Cli                 ← CLI ツール
```

### 新規パッケージ（計画）

```
Kafka.Context.Streaming           ← LINQ 共通抽象（新規）
Kafka.Context.Streaming.Flink     ← Flink SQL 方言（新規）
Kafka.Context.Streaming.Ksql      ← ksqlDB 方言（将来）
```

### 依存関係

```
Kafka.Context.Streaming.Flink
  └─ Kafka.Context.Streaming
       └─ Kafka.Context.Abstractions

Kafka.Context.Streaming.Ksql
  └─ Kafka.Context.Streaming
       └─ Kafka.Context.Abstractions
```

---

## CLI 仕様（dotnet-kafka-context）

### インストール

```powershell
dotnet tool install -g dotnet-kafka-context
dotnet tool update -g dotnet-kafka-context
```

### コマンド一覧

#### 1) schema scaffold

SR から C# 型を生成する。

```powershell
kafka-context schema scaffold --sr-url http://127.0.0.1:18081 --subject orders-value --output ./Generated
```

**オプション**
| オプション | 説明 | デフォルト |
|-----------|------|-----------|
| `--sr-url` | Schema Registry URL | 環境変数 or appsettings |
| `--subject` | SR subject 名 | 必須 |
| `--output` | 出力ディレクトリ | `./` |
| `--namespace` | 生成される namespace | `Kafka.Context.Generated` |
| `--style` | `record` or `class` | `record` |
| `--topic` | `[KafkaTopic]` の値 | subject から推測 |
| `--force` | 既存ファイルを上書き | `false` |
| `--dry-run` | プレビューのみ | `false` |

**生成される属性**
- `[KafkaTopic("<topic>")]`
- `[SchemaSubject("<subject>")]`
- `[SchemaFingerprint("<fingerprint>")]`

#### 2) schema verify

fingerprint の整合性を検証する（CI 推奨）。

```powershell
# 型から fingerprint を取得して検証
kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject orders-value --type "Kafka.Context.Generated.Order, MyApp"

# fingerprint を直接指定して検証（ビルド不要）
kafka-context schema verify --sr-url http://127.0.0.1:18081 --subject orders-value --fingerprint 0123abcd...
```

**Exit codes**
- `0`: 一致
- `4`: 不一致

#### 3) schema subjects

SR の subject 一覧を取得する。

```powershell
kafka-context schema subjects --sr-url http://127.0.0.1:18081
kafka-context schema subjects --sr-url http://127.0.0.1:18081 --prefix orders-
kafka-context schema subjects --sr-url http://127.0.0.1:18081 --json
```

### Fingerprint による整合性検証

**環境依存（検証しない）**
- Schema ID
- Version 番号
- 登録日時

**環境非依存（検証する）**
- フィールド名
- フィールド型
- nullable / required
- デフォルト値
- enum の symbols

**算出方法**: normalized JSON + SHA-256

### 運用フロー

```
開発   → scaffold 実行 → C# コード生成（fingerprint 埋め込み）
         ↓
CI     → ビルド + テスト
         ↓
検証   → デプロイ → 起動 → ProvisionAsync → SR fingerprint 照合
         ↓                                    ✓ or 💥
本番   → デプロイ → 起動 → ProvisionAsync → SR fingerprint 照合
                                              ✓ or 💥
```

---

## Streaming 設計

### 設計目標：EF Core との一貫性（入口 API は KafkaContext に寄せる）

**EF 開発者が「これ知ってる」と思える範囲を最大化**

| EF Core | Kafka.Context.Streaming |
|---------|------------------------|
| `DbContext` | `KafkaContext` |
| `DbSet<T>` | `EventSet<T>`（I/O） + `IStreamingQueryable<T>`（Query 内） |
| `IQueryable<T>` | `IStreamingQueryable<T>` |
| `.Where()` | `.Where()` |
| `.Select()` | `.Select()` |
| `.GroupBy()` | `.GroupBy()` |
| `.Join()` | `.Join()` |
| `.ToListAsync()` | `.ToAsyncEnumerable()` |

**同じ POCO を共有**
```csharp
[Table("orders")]
[KafkaTopic("orders")]
public class Order { ... }

// 同じ LINQ で両方を操作
dbContext.Orders.Where(...)
// Streaming Query は OnModelCreating 内で宣言する（下記）
```

### Query 宣言（OnModelCreating に集約）

**方針**
- Streaming の Query は `KafkaContext.OnModelCreating(...)` 内で宣言する（EF の Model 設定と同じ入口）。
- 実行時に “その場で Query を組み立てる” 方式は採らず、宣言済み Query を Provision/実行する。
- 宣言スタイルは `modelBuilder.Entity<TDerived>().ToQuery(...)` を基本形とする（To=Entity / From は `From<T>()` で明示）。
- `ToQuery(...)` は共通層では「宣言（式の保持）」までとし、方言固有側の Visitor で解釈して実クエリ（SQL/DDL/実行計画）を生成する。
- ObjectName（エンジン内識別子）の正規化は方言側で行う（引用/予約語/長さ制限など）。
- 同一の To（同一出力topic）に対する複数宣言は許可（INSERT 系の可能性）。ただし CTAS/作成系での二重作成は Fail-Fast。
- 定義変更（式の変更）は運用で対処し、本計画の対象外とする。
- CTAS/INSERT の方針: CTAS は TABLE、INSERT は STREAM（方言側で決定）。
- ループ防止: Join を含む全入力ソース（From/Join）のtopicを列挙し、出力topicと一致したら Fail-Fast（意図しない自己ループ防止）。

**エンティティの役割（入力/派生）**
- **Input（入力トピック）**: `EventSet<T>` により Produce/Consume を行う。
- **Derived（派生 / Query 結果）**: `Entity<TDerived>().ToQuery(...)` で定義される出力。出力topicは型名規約で決定し、別名が必要な場合のみ `KafkaTopicAttribute` で上書きする。

```csharp
public sealed class OrderContext : KafkaContext
{
    public EventSet<Order> Orders { get; private set; }

    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();

        // (案) Streaming Query の宣言（To=Entity / From を明示）
        modelBuilder.Entity<Order5m>().ToQuery(q => q
            .From<Order>()
            .Tumbling(o => o.CreatedAtUtc, new Windows { Minutes = new[] { 5 } })
            .GroupBy(o => o.CustomerId)
            .Select(g => new Order5m
            {
                CustomerId = g.Key,
                TotalAmount = g.Sum(x => x.Amount)
            }));
    }
}
```

### 方言選択（API は疑似コード扱い）

`options.UseStreaming<FlinkDialectProvider>()` のような形は **疑似コード** とし、最終的な登録ポイント（DI/Options/Context ctor）は実装段階で 1 つに固定する。

### 共通 API（Kafka.Context.Streaming）

**共通化の判断基準**
- 両方で同じ意味・同じ結果 → 共通
- 方言差が吸収可能 → 共通
- 方言差が意味を変える → 固有
- 一方にしかない → 固有

**Window は共通 API に含めない**
- Window（Tumbling/Hopping/Session 等）はセマンティクス差（event-time/processing-time、watermark、emit、within 等）が大きいため、共通層から除外し方言固有 API に寄せる。

**フィルタ・射影**
```csharp
.Where(x => x.Amount > 100)
.Select(x => new { x.Id, x.Amount })
```

**グループ化・集計**
```csharp
.GroupBy(x => x.CustomerId)
.Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })

// 基本集計: Count, Sum, Min, Max, Average
```

**JOIN（2 テーブル限定）**
```csharp
.Join(other, leftKey, rightKey, resultSelector)
.LeftJoin(other, leftKey, rightKey, resultSelector)
```

### Flink 固有 API

```csharp
namespace Kafka.Context.Streaming.Flink;

// 3 テーブル以上の JOIN
.Join(t2).Join(t3).On(...)

// 時間セマンティクス
.WithWatermark(o => o.EventTime, TimeSpan.FromSeconds(5))
.ProcessingTime()
.EventTime()

// Window（方言固有）
.TumblingWindow(TimeSpan.FromMinutes(5))
.HoppingWindow(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1))
.SessionWindow(TimeSpan.FromMinutes(30))

// 固有 JOIN
.IntervalJoin(...)
.TemporalJoin(...)
```

### ksqlDB 固有 API

```csharp
namespace Kafka.Context.Streaming.Ksql;

// 出力モード
.Emit(EmitMode.Changes)
.Emit(EmitMode.Final)

// タイムスタンプ指定
.WithTimestamp(o => o.CreatedAt)

// パーティション
.PartitionBy(o => o.CustomerId)

// Window（方言固有）
.Tumbling(o => o.CreatedAtUtc, new Windows { Minutes = new[] { 5 } })
.Hopping(o => o.CreatedAtUtc, windowSize: TimeSpan.FromMinutes(5), hopInterval: TimeSpan.FromMinutes(1), grace: null)
.Session(o => o.CreatedAtUtc, gap: TimeSpan.FromMinutes(30))
```

### DDL Provisioning

```csharp
// デフォルト: IF NOT EXISTS（冪等）
await ctx.ProvisionStreamingAsync();

// 明示的に OR REPLACE（Flink のみ）
await ctx.ProvisionStreamingAsync(options =>
{
    options.Streams["orders"].Mode = CreateMode.OrReplace;
});
```

---

## 責務分離

### Kafka.Context の責務

```
やること:
  ├─ Produce / Consume
  ├─ Contract 管理（SR + fingerprint）
  ├─ Flink / ksqlDB 連携
  └─ OpenTelemetry メトリクス出力

やらないこと:
  ├─ Dashboard UI
  ├─ 独自可視化
  ├─ 独自監視
  └─ 複雑なミドルウェア
```

### ツールとの責務分離

```
開発者:       EF の知識で Kafka を使う
テックリード: appsettings.json を設計
AI:          設定の生成・検証を支援
Datadog 等:  監視・可視化
```

---

## 実装ロードマップ

### Phase 1: Streaming 基盤（Flink 優先）

1. `Kafka.Context.Streaming` 共通抽象
2. `Kafka.Context.Streaming.Flink` 基本実装
3. 式ツリー → Flink SQL 変換
4. Flink SQL Gateway クライアント

### Phase 2: 機能拡充

1. JOIN（2 テーブル）
2. ウィンドウ処理
3. DDL 生成 + ProvisionStreamingAsync

### Phase 3: ksqlDB 対応

1. `Kafka.Context.Streaming.Ksql`
2. Kafka.Ksql.Linq からの知見移行
3. バックエンド切替テスト

### Kafka.Ksql.Linq との関係

```
現状:
  Kafka.Ksql.Linq → 独立したライブラリ（30k steps）

将来:
  Kafka.Context.Streaming.Ksql → 式変換ロジックを移植
  Kafka.Ksql.Linq → メンテナンスモード
```

---

## 市場機会

### .NET Flink ライブラリの現状

| ライブラリ | アプローチ | LINQ |
|-----------|----------|------|
| FlinkDotnet | Fluent API | ✗ |
| HEF.Flink | ADO.NET | ✗ |
| FLink.CSharp | Flink ポート | ✗ |

**空いている領域: LINQ → Flink SQL 変換**

### ksqlDB → Flink 移行需要

- Confluent は Flink に投資シフト
- ksqlDB は積極的開発縮小
- 移行はこれから本格化
- Kafka.Context.Streaming はバックエンド切替で移行を容易にする

---

## 型変換ルール

### Avro → C#

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
| `enum` | `enum` |
| `record`（ネスト） | nested record/class |
| `logical:decimal` | `decimal` |
| `logical:uuid` | `Guid` |
| `logical:date` | `DateOnly` |
| `logical:timestamp-millis` | `DateTime` |
| `logical:timestamp-micros` | `DateTime` |

---

## 付録：設計原則まとめ

1. **薄いラッパー** - 機能を最小限に保ち、複雑さは外部に委譲
2. **Contract-First** - スキーマ契約を一級市民として扱う
3. **Fail-Fast** - 起動時に不整合を検出して即座に失敗
4. **EF 一貫性** - EF 開発者が「これ知ってる」と思える IF
5. **冪等性** - 何度実行しても安全（IF NOT EXISTS）
6. **責務分離** - 監視は Datadog、UI は作らない

# Kafka.Context.Streaming 仕様書（最終版）

この仕様は「既存インフラへ接続するなら `EventSet<T>` のみ、`ToQuery` は `ProvisionStreamingAsync` で登録するクエリ定義の宣言」を前提にする。

## 概要

Kafka.Context.Streaming は、ストリーミングクエリの共通抽象層を提供するパッケージである。LINQ 式ツリーを論理プランに変換し、Flink / ksqlDB などの方言プロバイダーへのブリッジとなる。

---

## 責務範囲

### 責務（IN SCOPE）

| 責務 | 説明 |
|------|------|
| IStreamingQueryable\<T\> の定義 | LINQ 拡張メソッド群の提供 |
| LINQ → 論理プラン変換 | 式ツリーを正規化された要素に分解 |
| 方言プロバイダー IF の定義 | 拡張ポイントの提供 |
| 方言制約 IF の定義 | 制約検証の抽象化 |
| 警告の収集 | 制約違反の検出と警告情報の付与 |
| ToQuery 拡張メソッドの提供 | EntityModelBuilder への拡張（To を明示する） |
| Query 定義の格納 | StreamingQueryRegistry による管理 |

### 責務外（OUT OF SCOPE）

| 責務外 | 担当 |
|--------|------|
| SQL 生成 | Streaming.Flink / Streaming.Ksql |
| DDL 生成 | Streaming.Flink / Streaming.Ksql |
| クエリ実行 | Streaming.Flink / Streaming.Ksql |
| Window 処理 | 方言固有（セマンティクス差異大） |
| Fail-Fast 検証 | 方言プロバイダー登録時 |
| Topic / SR Provision | Kafka.Context（既存 ProvisionAsync） |

---

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│              Kafka.Context.Streaming                    │
├─────────────────────────────────────────────────────────┤
│  IN:  式ツリー（LINQ Expression）                       │
│  OUT: 論理プラン（QueryPlanResult）                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  EntityModelBuilderExtensions                   │   │
│  │  - ToQuery() 拡張メソッド（To=Entity を明示）     │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │  StreamingQueryRegistry                         │   │
│  │  - QueryDefinition の格納                       │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │  IStreamingQueryable<T>                         │   │
│  │  - Where, Select, Distinct, GroupBy,           │   │
│  │    Having, OrderBy, Join, LeftJoin             │   │
│  └─────────────────────────────────────────────────┘   │
│                          ↓                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │  QueryPlanResult                                │   │
│  │  - QueryPlan（論理プラン）                      │   │
│  │  - Warnings（警告リスト）                       │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
└─────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────┐  ┌──────────────────────────┐
│  Streaming.Flink         │  │  Streaming.Ksql          │
│  - FlinkDialectProvider  │  │  - KsqlDialectProvider   │
│  - FlinkQueryPlanVisitor │  │  - KsqlQueryPlanVisitor  │
│  - SQL 生成              │  │  - SQL 生成              │
│  - DDL 生成              │  │  - DDL 生成              │
│  - 実行 + Fail-Fast      │  │  - 実行 + Fail-Fast      │
└──────────────────────────┘  └──────────────────────────┘
```

---

## 共通層 vs 方言固有の境界（確定）

### 論理プラン要素

| 要素 | 共通 | 方言固有 | 理由 |
|------|:----:|:--------:|------|
| Source | Yes |  | トピック参照は普遍 |
| Filter | Yes |  | WHERE は普遍 |
| Project | Yes |  | SELECT は普遍 |
| Distinct | Yes |  | DISTINCT は普遍 |
| Aggregate | Yes |  | GROUP BY + 基本集計は普遍 |
| Having | Yes |  | HAVING は普遍 |
| Order | Yes |  | ORDER BY は普遍 |
| Join (Inner/Left) | Yes |  | 基本 JOIN は普遍 |
| Limit |  | Yes | ストリーミングでのセマンティクス不明確 |
| Window |  | Yes | セマンティクス差異大 |
| Watermark |  | Yes | Flink 固有 |
| Emit |  | Yes | ksqlDB 固有 |
| IntervalJoin |  | Yes | Flink 固有 |
| TemporalJoin |  | Yes | Flink 固有 |
| WITHIN |  | Yes | ksqlDB 固有 |
| CountDistinct |  | Yes | 関数名が方言で異なる |

### 共通 API に含まれるもの

- Where, Select, Distinct
- GroupBy, Having
- OrderBy, OrderByDescending, ThenBy, ThenByDescending
- Join, LeftJoin

### 共通 API に含まれないもの（方言固有）

- Limit / Take（ストリーミングでのセマンティクス不明確）
- Window（セマンティクス差異大）
- CountDistinct（関数名が方言で異なる：Flink は `COUNT(DISTINCT x)`、ksqlDB は `COUNT_DISTINCT(x)`）

### Window を共通層から除外する理由

```
Flink:
  - Watermark 必須
  - Processing Time / Event Time 明示
  - Window Assigners（独自概念）
  
ksqlDB:
  - EMIT CHANGES / FINAL
  - WITHIN 句（JOIN 用）
  - 暗黙の Row Time

→ 共通化すると「最小公約数」になり方言の強みを殺す
```

### Flink の event-time / watermark（運用ルール）

Flink で TUMBLE/HOP を使う場合、`DESCRIPTOR(ts)` に渡す **timestamp 列**と **watermark** は入力テーブル定義（`CREATE TABLE ... WATERMARK FOR ...`）に属する。
そのため、Window 入力となるトピック型は `OnModelCreating` で Flink 固有に source 定義できるようにする。

```csharp
modelBuilder.Entity<WindowInput>()
    .FlinkSource(s => s.EventTimeColumn(x => x.EventTime, watermarkDelay: TimeSpan.FromSeconds(5)));
```

### Flink Kafka connector format（運用ルール）

本プロジェクトの Flink 用 DDL 生成は **Avro（Confluent 互換）** のみをサポートする（`format = 'avro-confluent'` を前提）。
他フォーマット（JSON 等）は対象外とする。

### Limit を共通層から除外する理由

```
バッチ処理:
  SELECT * FROM orders LIMIT 10
  → 最初の10件を取得して終了 ✓

ストリーミング処理:
  無限に流れるストリームに対して「最初の10件」とは？
  → セマンティクスが不明確

方言ごとの動作:
  - Flink (Batch): 動作する
  - Flink (Streaming): 限定的 / 非推奨
  - ksqlDB Pull Query: 動作する
  - ksqlDB Push Query: サポートなし
```

---

## 論理プラン定義

### QueryPlan（基底）

```csharp
namespace Kafka.Context.Streaming;

public abstract record QueryPlan;
```

### 共通プラン要素

```csharp
namespace Kafka.Context.Streaming;

public record SourcePlan(
    string Topic, 
    Type ElementType
) : QueryPlan;

public record FilterPlan(
    QueryPlan Source, 
    LambdaExpression Predicate
) : QueryPlan;

public record ProjectPlan(
    QueryPlan Source, 
    LambdaExpression Selector
) : QueryPlan;

public record DistinctPlan(
    QueryPlan Source
) : QueryPlan;

public record AggregatePlan(
    QueryPlan Source, 
    LambdaExpression KeySelector, 
    IReadOnlyList<AggregateFunction> Aggregates
) : QueryPlan;

public record HavingPlan(
    QueryPlan Source,
    LambdaExpression Predicate
) : QueryPlan;

public record OrderPlan(
    QueryPlan Source, 
    IReadOnlyList<OrderElement> Orderings
) : QueryPlan;

public record JoinPlan(
    QueryPlan Left, 
    QueryPlan Right, 
    JoinType Type, 
    LambdaExpression LeftKey, 
    LambdaExpression RightKey
) : QueryPlan;
```

### 方言固有プラン要素（参考）

```csharp
// Kafka.Context.Streaming.Flink
public record FlinkWindowPlan(
    QueryPlan Source, 
    FlinkWindowSpec Window, 
    WatermarkSpec Watermark
) : QueryPlan;

public record FlinkLimitPlan(
    QueryPlan Source, 
    int Count
) : QueryPlan;

// Kafka.Context.Streaming.Ksql
public record KsqlWindowPlan(
    QueryPlan Source, 
    KsqlWindowSpec Window, 
    EmitMode Emit
) : QueryPlan;
```

---

## 補助型定義

### OrderElement

```csharp
namespace Kafka.Context.Streaming;

public record OrderElement(
    LambdaExpression KeySelector,
    bool Descending
);
```

**設計判断：** NULLS FIRST / NULLS LAST は共通層に含めない（方言固有）

### AggregateFunction

```csharp
namespace Kafka.Context.Streaming;

public record AggregateFunction(
    AggregateFunctionType Type,
    LambdaExpression? Selector,  // Count(*) の場合 null
    string OutputName
);
```

---

## 集計関数定義

### AggregateFunctionType（SQL 標準ベース）

```csharp
namespace Kafka.Context.Streaming;

public enum AggregateFunctionType
{
    // 基本
    Count,
    LongCount,
    Sum,
    Min,
    Max,
    Avg,
    
    // SQL 標準（統計）
    StdDev,
    StdDevPop,
    Variance,
    VariancePop
}
```

---

## 演算子定義

### OperatorType

```csharp
namespace Kafka.Context.Streaming;

public enum OperatorType
{
    // 比較
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    
    // 論理
    And,
    Or,
    Not,
    
    // 算術
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    
    // 文字列
    Concat,
    Like,
    
    // NULL
    IsNull,
    IsNotNull,
    Coalesce
}
```

---

## JOIN 種別定義

### JoinType

```csharp
namespace Kafka.Context.Streaming;

public enum JoinType
{
    Inner,
    Left,
    Right,
    Full,
    Cross
}
```

---

## EntityModelBuilder の配置（確定）

### 設計方針

- EntityModelBuilder<T> は Abstractions に配置（sealed、変更なし）
- `IModelBuilder.Entity<T>()` はモデル上の型（Input/To）を宣言する入口
- `Entity<TDerived>().ToQuery(...)` により **To（出力）** と **From（入力）** をコード上で明示する
- endpoint（出力topic）は `TDerived` の型名規約で固定し、別名が必要な場合のみ `KafkaTopicAttribute` で上書きする
- `ToQuery(...)` は Streaming 層に **拡張メソッド** として配置し、Query 定義を Context インスタンス単位の Registry に登録する
- 依存方向：Streaming → Abstractions（正しい方向）

### Kafka.Context.Abstractions（変更なし）

```csharp
namespace Kafka.Context.Abstractions;

public sealed class EntityModelBuilder<T> where T : class
{
    private readonly IModelBuilder _modelBuilder;
    
    internal EntityModelBuilder(IModelBuilder modelBuilder)
    {
        _modelBuilder = modelBuilder;
    }

    internal IModelBuilder ModelBuilder => _modelBuilder;
    
    // 基本機能のみ（ToQuery なし）
}
```

**設計判断：** `readOnly` / `writeOnly` 引数は採用しない（利用者責任）

```csharp
// IModelBuilder
public interface IModelBuilder
{
    EntityModelBuilder<T> Entity<T>() where T : class;  // 引数なし
}
```

### Kafka.Context.Streaming（拡張メソッド / To=Entity）

```csharp
namespace Kafka.Context.Streaming;

public static class EntityModelBuilderStreamingExtensions
{
    public static EntityModelBuilder<TDerived> ToQuery<TDerived>(
        this EntityModelBuilder<TDerived> builder,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode = StreamingOutputMode.Changelog,
        StreamingSinkMode sinkMode = StreamingSinkMode.AppendOnly)
        where TDerived : class
    {
        var registry = ((IStreamingQueryRegistryProvider)builder.ModelBuilder).StreamingQueries;
        registry.Add(typeof(TDerived), queryFactory, outputMode, sinkMode);

        return builder;
    }
}
```

---

## ToQuery の実現方式（確定）

### 設計方針

「override」ではなく「Visitor で解釈」が正。

```
ToQuery（共通・拡張メソッド）
    ↓ 式を保持（StreamingQueryRegistry）
ProvisionStreamingAsync
    ↓ 方言プロバイダーに渡す
FlinkQueryPlanVisitor / KsqlQueryPlanVisitor
    ↓ 論理プラン → SQL 変換
エンジンに登録
```

### Visitor インターフェース

```csharp
namespace Kafka.Context.Streaming;

public interface IQueryPlanVisitor
{
    string GenerateSql(QueryPlan plan);
    string GenerateDdl(QueryPlan plan, string objectName, string outputTopic);
}
```

### 方言 Visitor（例：Flink）

```csharp
namespace Kafka.Context.Streaming.Flink;

public class FlinkQueryPlanVisitor : IQueryPlanVisitor
{
    public string GenerateSql(QueryPlan plan)
    {
        return plan switch
        {
            SourcePlan source => VisitSource(source),
            FilterPlan filter => VisitFilter(filter),
            ProjectPlan project => VisitProject(project),
            DistinctPlan distinct => VisitDistinct(distinct),
            AggregatePlan aggregate => VisitAggregate(aggregate),
            HavingPlan having => VisitHaving(having),
            OrderPlan order => VisitOrder(order),
            JoinPlan join => VisitJoin(join),
            FlinkWindowPlan window => VisitFlinkWindow(window),
            FlinkLimitPlan limit => VisitFlinkLimit(limit),
            _ => throw new NotSupportedException($"Plan type {plan.GetType()} not supported")
        };
    }
    
    public string GenerateDdl(QueryPlan plan, string objectName, string outputTopic)
    {
        var sql = GenerateSql(plan);
        return $"CREATE TABLE IF NOT EXISTS {objectName} /* topic={outputTopic} */ AS {sql}";
    }
    
    private string VisitSource(SourcePlan source) { /* Flink SQL */ }
    private string VisitFilter(FilterPlan filter) { /* Flink SQL */ }
    // ...
}
```

---

## 方言選択（確定）

### 設計方針

Context 自体が方言を決定する（OnConfiguring で指定）。

```
理由:
  - Context はその特性に依存すべき
  - 途中で方言を変更するのは危険
  - 「契約で壊れない」原則と一致
```

### KafkaContextOptionsBuilder

```csharp
namespace Kafka.Context;

public class KafkaContextOptionsBuilder
{
    private IStreamingDialectProvider? _streamingDialect;
    
    public KafkaContextOptionsBuilder UseStreaming<TDialect>() 
        where TDialect : IStreamingDialectProvider, new()
    {
        _streamingDialect = new TDialect();
        return this;
    }
    
    internal KafkaContextOptions Build() => new(_streamingDialect);
}

public class KafkaContextOptions
{
    public IStreamingDialectProvider? StreamingDialect { get; }
    
    internal KafkaContextOptions(IStreamingDialectProvider? streamingDialect)
    {
        StreamingDialect = streamingDialect;
    }
}
```

### OnConfiguring での指定

```csharp
public class OrderContext : KafkaContext
{
    protected override void OnConfiguring(KafkaContextOptionsBuilder builder)
    {
        builder.UseStreaming<FlinkDialectProvider>();
    }
    
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
        
        modelBuilder.Entity<Order5m>().ToQuery(q => q
            .From<Order>()
            .Where(o => o.Status == "completed")
            .GroupBy(o => o.CustomerId)
            .Select(g => new Order5m
            {
                CustomerId = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .Having(x => x.Total > 1000));
    }
}
```

### 方言を変えたい場合

```csharp
// Flink 用
public class OrderContextFlink : KafkaContext
{
    protected override void OnConfiguring(KafkaContextOptionsBuilder builder)
    {
        builder.UseStreaming<FlinkDialectProvider>();
    }
    
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // 共通のモデル定義
    }
}

// ksqlDB 用（移行期など）
public class OrderContextKsql : KafkaContext
{
    protected override void OnConfiguring(KafkaContextOptionsBuilder builder)
    {
        builder.UseStreaming<KsqlDialectProvider>();
    }
    
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // 共通のモデル定義
    }
}
```

---

## Query 定義の格納（確定）

### 設計方針

- StreamingQueryDefinition は Context 単位で管理する（static は使わない）
- 実装差し替え可能にするため、Context から参照する型は `IStreamingQueryRegistry` とする

### StreamingQueryDefinition

```csharp
namespace Kafka.Context.Streaming;

public sealed record StreamingQueryDefinition(
    Type DerivedType,
    Func<IStreamingQueryBuilder, IStreamingQueryable> QueryFactory,
    StreamingOutputMode OutputMode,
    StreamingSinkMode SinkMode);
```

### IStreamingQueryRegistry

```csharp
namespace Kafka.Context.Streaming;

public interface IStreamingQueryRegistry
{
    void Add(
        Type derivedType,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode,
        StreamingSinkMode sinkMode);
    IReadOnlyList<StreamingQueryDefinition> GetAll();
}

// ModelBuilder implementation should provide access to the context-scoped registry.
internal interface IStreamingQueryRegistryProvider
{
    IStreamingQueryRegistry StreamingQueries { get; }
}
```

### StreamingQueryRegistry

```csharp
namespace Kafka.Context.Streaming;

public sealed class StreamingQueryRegistry : IStreamingQueryRegistry
{
    private readonly List<StreamingQueryDefinition> _definitions = new();
    
    public void Add(
        Type derivedType,
        Func<IStreamingQueryBuilder, IStreamingQueryable> queryFactory,
        StreamingOutputMode outputMode,
        StreamingSinkMode sinkMode)
    {
        _definitions.Add(new StreamingQueryDefinition(derivedType, queryFactory, outputMode, sinkMode));
    }
    
    public IReadOnlyList<StreamingQueryDefinition> GetAll() => _definitions;
}
```

### OutputMode（Changelog / Final）

- `OutputMode` は `ToQuery(..., outputMode: ...)` で**クエリ単位**に指定する。
- `Changelog` はデフォルトで、通常のストリーミング出力を指す。
- `Final` は「ウィンドウ確定後のみ出力（最終結果）」の意図を表す（例: ksqlDB の `EMIT FINAL`）。
  - MVP では **`Final` は Window（TUMBLE/HOP）必須**とし、非 Window は Fail-Fast とする。

### SinkMode（AppendOnly / Upsert）

- `SinkMode` は `ToQuery(..., sinkMode: ...)` で**クエリ単位**に指定する。
- `AppendOnly` はデフォルトで、追加書き込み（append）を前提にする。
- `Upsert` はキーによる更新（upsert）を前提にする。
  - Flink の実運用では、sink（コネクタ/テーブル定義）と topic 運用（compaction 等）に依存するため、共通層は「意図」として保持する。
  - MVP では **`Upsert` は Window（TUMBLE/HOP）+ CTAS（TABLE）必須**とし、それ以外は Fail-Fast とする。

---

## Provision の責務（確定）

### 設計方針

既存の ProvisionAsync と Streaming 用の ProvisionStreamingAsync を**完全分離**する。

```csharp
// Topic + Schema Registry（既存）
await ctx.ProvisionAsync();

// Streaming Query 登録（新規）
await ctx.ProvisionStreamingAsync();
```

### 責務境界

| メソッド | 責務 | 層 |
|----------|------|-----|
| ProvisionAsync | Topic 作成 + Schema Registry 登録 | Kafka.Context |
| ProvisionStreamingAsync | Streaming Query 登録 | Kafka.Context.Streaming |

### ProvisionStreamingAsync

```csharp
namespace Kafka.Context;

public abstract class KafkaContext
{
    private IStreamingDialectProvider? _dialectProvider;
    private readonly IStreamingQueryRegistry _streamingQueries = new StreamingQueryRegistry();
    
    protected KafkaContext()
    {
        var optionsBuilder = new KafkaContextOptionsBuilder();
        OnConfiguring(optionsBuilder);
        var options = optionsBuilder.Build();
        _dialectProvider = options.StreamingDialect;
        
        BuildModel();
    }
    
    protected virtual void OnConfiguring(KafkaContextOptionsBuilder builder) { }
    
    protected virtual void OnModelCreating(IModelBuilder modelBuilder) { }
    
    private void BuildModel()
    {
        var modelBuilder = new ModelBuilder();
        OnModelCreating(modelBuilder);
    }
    
    public async Task ProvisionStreamingAsync(CancellationToken ct = default)
    {
        if (_dialectProvider == null)
        {
            throw new InvalidOperationException(
                "Streaming dialect not configured. Call UseStreaming<T>() in OnConfiguring.");
        }
        
        var definitions = _streamingQueries.GetAll();
        
        // (optional) dialect-specific catalog DDL (e.g., Flink CREATE TABLE ... WITH (...) for sources/sinks)
        // If the dialect provider implements IStreamingCatalogDdlProvider, ProvisionStreamingAsync may emit those DDLs first.

        foreach (var definition in definitions)
        {
            // QueryFactory から IStreamingQueryable を生成し、論理プラン（StreamingQueryPlan）へ変換する（共通層の責務）
            var plan = BuildPlan(definition.QueryFactory);
            var kind = DetermineKind(plan); // STREAM(INSERT) or TABLE(CTAS)
            var outputTopic = ResolveOutputTopic(definition.DerivedType); // 既定は DerivedType 名の規則
            var objectName = _dialectProvider.NormalizeObjectName(outputTopic);

            // DDL 生成（方言層の責務）
            var ddl = _dialectProvider.GenerateDdl(plan, kind, definition.OutputMode, objectName, outputTopic);
            
            // エンジンに登録（失敗時は即例外）
            await _dialectProvider.ExecuteAsync(ddl, ct);
        }
    }
}
```

### Fail-Fast の挙動

| 失敗箇所 | 挙動 |
|----------|------|
| 1つ目の Query 失敗 | 即例外、後続は実行されない |
| N個目の Query 失敗 | 即例外、1〜N-1 は登録済み |

### 実行順序（重複時）

- INSERT（STREAM）は **宣言順** に実行する
- CTAS（TABLE）は、実行前の事前検証で **同一 `ObjectName` が2回出たら Fail-Fast**（1件も実行しない）

### ロールバック

**方針：ロールバックなし（登録のみ）**

```
理由:
  - ロールバック失敗時の担保ができない
  - DDL は冪等（IF NOT EXISTS）で再実行可能
  - シンプルな実装を維持
```

---

## 全体運用（確定）

### 運用の前提

- Kafka topic / Schema Registry の Provision は `KafkaContext.ProvisionAsync` の責務（Streaming は関与しない）
- `ProvisionStreamingAsync` は **クエリ登録のみ**（ロールバックしない）
- Derived（`ToQuery` の出力型）の出力topicは型名規約で決定し、必要なら `KafkaTopicAttribute` で上書きする
- Attach（既存インフラへ接続するだけ）の場合は `EventSet<T>` のみを利用し、`ToQuery` は使わない
- `OnModelCreating` で `ToQuery` を宣言することは「Provision対象の Query を定義する」行為である

### 名前の役割分離（重要）

`topic name = stream/table name` をルール化しない。Kafka の実体（topic）と、エンジン内オブジェクト（stream/table/view）を別名として扱う。

| 対象 | 種類 | 推奨の名前ソース |
|------|------|------------------|
| Kafka input | topic | `KafkaTopicAttribute`（入力型） |
| Engine derived object | stream/table/view | `ObjectName`（Derived型の `KafkaTopicAttribute` から規約で決定） |

入力topicの参照方法（SOURCE名、引用、正規化など）は方言プロバイダー側の責務とする。

### 冪等性（CREATE IF NOT EXISTS）

- 方言側は可能な範囲で `CREATE ... IF NOT EXISTS` に変換する
- 冪等性の単位は **ObjectName（エンジン内オブジェクト名）**
- ObjectName を安定させる限り、Provision の再実行は安全に近づく

### ObjectName 正規化（方言側）

- ObjectName の最終的な正規化（引用、予約語回避、長さ制限、許容文字など）は **方言プロバイダー側** で解釈する。
- 共通層は「topic からの推奨名」を保持するが、方言側が上書きしてよい。

### StreamingQueryDefinition 重複（許可）と Fail 条件

- 同一 `TDerived`（同一 OutputTopic）に対して複数の `ToQuery` を宣言することを **許可** する。
  - 想定: `INSERT INTO <existing>` 相当で複数の流入（fan-in）を構成するケース。
- ただし方言側が `CTAS/CSAS`（作成）を選択する場合、同一 `ObjectName` の二重作成は **Fail-Fast** とする。
- 方言ルール: **CTAS は TABLE**、**INSERT は STREAM** を作成/利用する（ksqlDB の CREATE TABLE AS SELECT / CREATE STREAM AS SELECT / INSERT INTO に対応）。
- 自動判定（暫定ルール）: QueryPlan に `AggregatePlan` / `HavingPlan`（= GroupBy/集計/Having）を含む場合は **TABLE(CTAS)**、それ以外は **STREAM(INSERT)** とする。
- 定義変更（式の変更）は運用で対処し、本仕様の対象外とする。

### ループ防止（必須）

- `From<TSource>` の入力topic と `TDerived` の出力topic が同一になる場合は **Fail-Fast** とする（意図しない自己ループ防止）。
- 判定粒度: JOIN を含む場合は、JOIN に登場する **全ての入力ソース（From/Join）** を入力topicとして列挙し、出力topicと一致したら Fail-Fast とする。
- 判定は方言プロバイダーが `BuildPlan` 後に `SourcePlan` 等から入力topicを列挙し、`definition.OutputTopic` と比較して行う。

### Window + JOIN 制約（確定）

Window（TUMBLE/HOP）と JOIN はセマンティクスが破綻しやすいため、当面は **許可パターンを最小** に限定し、それ以外は方言側で Fail-Fast とする。

- **許可**:
  - **片側が Window 集計結果（CTAS）** の場合のみ JOIN を許可する（例: `Agg(Order)` JOIN `Customer`）
  - JOIN は **INNER のみ**
  - JOIN の `ON` は **キー等値のみ**（例: `o.CustomerId == c.Id`）
- **禁止（当面）**:
  - stream-stream JOIN（両側が生入力/Window入力の JOIN）
  - JOIN → Window（JOIN した結果に対して Window 集計を適用）
  - Window 同士の JOIN（CTAS × CTAS）

### 変更手順（Query定義を変えたい場合）

`IF NOT EXISTS` は “更新” を行わないため、定義変更は次のいずれかで扱う。

- 推奨: ObjectName を変更して新規登録（旧オブジェクトの drop/cleanup は手動運用）
- 方言固有（将来）: `OR REPLACE` / `CREATE OR REPLACE` を許可する場合は方言パッケージ側で明示オプション化する

### 削除/無効化

- `ProvisionStreamingAsync` は drop を行わない
- 既存オブジェクトの削除はエンジン管理（手動DDL/運用手順）とする

## 命名規約

### 設計方針

属性で明示 + 未指定時は規約でフォールバック。

### 属性定義

```csharp
namespace Kafka.Context;

// 既存
[AttributeUsage(AttributeTargets.Class)]
public class KafkaTopicAttribute : Attribute
{
    public string Name { get; }
    public KafkaTopicAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Class)]
public class SchemaSubjectAttribute : Attribute
{
    public string Name { get; }
    public SchemaSubjectAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Class)]
public class DeadLetterTopicAttribute : Attribute
{
    public string Name { get; }
    public DeadLetterTopicAttribute(string name) => Name = name;
}
```

### 規約（未指定時のフォールバック）

| 項目 | 規約 | 例（Order5m） |
|------|------|---------------|
| Object 名（engine object） | normalize(Topic) | `orders-5m` → `orders_5m` |

### NamingConvention

```csharp
namespace Kafka.Context;

public static class NamingConvention
{
    public static string GetTopicName<T>() => GetTopicName(typeof(T));

    public static string GetTopicName(Type type)
    {
        var attr = type.GetCustomAttribute<KafkaTopicAttribute>();
        if (attr != null) return attr.Name;

        // Default endpoint topic name by convention (type-name -> kebab-case).
        return ToKebabCase(type.Name);
    }

    public static string GetObjectNameFromTopic(string topic)
    {
        // Minimal normalization: topic-name -> topic_name (dialects may require quoting).
        return topic.Replace('-', '_');
    }

    private static string ToKebabCase(string name)
    {
        return Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }
    
    private static string ToSnakeCase(string name)
    {
        return Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }
}
```

### 利用例

```csharp
// 規約: 型名からtopic名を決定（Order5m -> order-5m）
public class Order5m { ... }

// 別名が必要な場合は明示指定
[KafkaTopic("orders-5m")]
public class Order5mLegacy { ... }
```

---

## インターフェース定義

### IStreamingQueryable<T>

```csharp
namespace Kafka.Context.Streaming;

public interface IStreamingQueryable<T>
{
}

public interface IStreamingOrderedQueryable<T> : IStreamingQueryable<T>
{
}

public interface IStreamingGroupedQueryable<TKey, T>
{
}

public interface IStreamingGrouping<TKey, T>
{
    TKey Key { get; }
}
```

### IStreamingQueryBuilder

```csharp
namespace Kafka.Context.Streaming;

public interface IStreamingQueryBuilder
{
    IStreamingQueryable<T> From<T>() where T : class;
}
```

### IQueryPlanVisitor

```csharp
namespace Kafka.Context.Streaming;

public interface IQueryPlanVisitor
{
    string GenerateSql(QueryPlan plan);
    string GenerateDdl(QueryPlan plan, string queryName);
}
```

---

## 拡張メソッド定義

### StreamingQueryableExtensions

```csharp
namespace Kafka.Context.Streaming;

public static class StreamingQueryableExtensions
{
    // === フィルタ ===
    public static IStreamingQueryable<T> Where<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, bool>> predicate
    );

    // === 射影 ===
    public static IStreamingQueryable<TResult> Select<T, TResult>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TResult>> selector
    );

    // === 重複除去 ===
    public static IStreamingQueryable<T> Distinct<T>(
        this IStreamingQueryable<T> source
    );

    // === グループ化 ===
    public static IStreamingGroupedQueryable<TKey, T> GroupBy<T, TKey>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TKey>> keySelector
    );

    public static IStreamingQueryable<TResult> Select<TKey, T, TResult>(
        this IStreamingGroupedQueryable<TKey, T> source,
        Expression<Func<IStreamingGrouping<TKey, T>, TResult>> selector
    );

    // === 集計後フィルタ ===
    public static IStreamingQueryable<T> Having<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, bool>> predicate
    );

    // === ソート ===
    public static IStreamingOrderedQueryable<T> OrderBy<T, TKey>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TKey>> keySelector
    );

    public static IStreamingOrderedQueryable<T> OrderByDescending<T, TKey>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TKey>> keySelector
    );

    public static IStreamingOrderedQueryable<T> ThenBy<T, TKey>(
        this IStreamingOrderedQueryable<T> source,
        Expression<Func<T, TKey>> keySelector
    );

    public static IStreamingOrderedQueryable<T> ThenByDescending<T, TKey>(
        this IStreamingOrderedQueryable<T> source,
        Expression<Func<T, TKey>> keySelector
    );

    // === JOIN ===
    public static IStreamingQueryable<TResult> Join<TOuter, TInner, TKey, TResult>(
        this IStreamingQueryable<TOuter> outer,
        IStreamingQueryable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner, TResult>> resultSelector
    );

    public static IStreamingQueryable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
        this IStreamingQueryable<TOuter> outer,
        IStreamingQueryable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector,
        Expression<Func<TOuter, TInner?, TResult>> resultSelector
    );
}
```

**設計判断：Join API**
- 公開 API は ResultSelector を含める（標準 LINQ 互換）
- 内部で JoinPlan + ProjectPlan に分離

---

## 集計関数拡張メソッド

### StreamingGroupingExtensions

```csharp
namespace Kafka.Context.Streaming;

public static class StreamingGroupingExtensions
{
    // === Count ===
    public static int Count<TKey, T>(
        this IStreamingGrouping<TKey, T> source
    );

    public static int Count<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, bool>> predicate
    );

    public static long LongCount<TKey, T>(
        this IStreamingGrouping<TKey, T> source
    );

    // === Sum ===
    public static int Sum<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, int>> selector
    );

    public static long Sum<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, long>> selector
    );

    public static double Sum<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );

    public static decimal Sum<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, decimal>> selector
    );

    // === Min ===
    public static TResult Min<TKey, T, TResult>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, TResult>> selector
    );

    // === Max ===
    public static TResult Max<TKey, T, TResult>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, TResult>> selector
    );

    // === Avg ===
    public static double Avg<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, int>> selector
    );

    public static double Avg<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, long>> selector
    );

    public static double Avg<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );

    public static decimal Avg<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, decimal>> selector
    );

    // === 統計（SQL 標準） ===
    public static double StdDev<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );

    public static double StdDevPop<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );

    public static double Variance<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );

    public static double VariancePop<TKey, T>(
        this IStreamingGrouping<TKey, T> source,
        Expression<Func<T, double>> selector
    );
}
```

---

## Where / Having の使い分け

### 設計方針

自動判定を廃止し、明示的な使い分けを採用する。

| API | 論理プラン | 用途 |
|-----|-----------|------|
| `Where(...)` | 常に FilterPlan | 集計前のフィルタ（WHERE） |
| `Having(...)` | 常に HavingPlan | 集計後のフィルタ（HAVING） |

### 利用例

```csharp
// 正しい使い方
orders
    .Where(o => o.Status == "completed")           // → FilterPlan (WHERE)
    .GroupBy(o => o.CustomerId)
    .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
    .Having(x => x.Total > 1000)                   // → HavingPlan (HAVING)

// 誤用（方言層で警告）
orders
    .GroupBy(o => o.CustomerId)
    .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
    .Where(x => x.Total > 1000)                    // → FilterPlan（警告対象）
```

### 論理プランの流れ

```
SourcePlan(orders)
    ↓
FilterPlan(status == "completed")     ← WHERE
    ↓
AggregatePlan(group by customer_id)
    ↓
ProjectPlan(select key, sum)
    ↓
HavingPlan(total > 1000)              ← HAVING
```

---

## 方言制約インターフェース

### IDialectConstraints

```csharp
namespace Kafka.Context.Streaming;

public interface IDialectConstraints
{
    /// <summary>集計関数 × 型の制約検証</summary>
    ConstraintResult CheckAggregate(AggregateFunctionType function, Type argumentType);
    
    /// <summary>演算子 × 型の制約検証</summary>
    ConstraintResult CheckOperator(OperatorType op, Type leftType, Type? rightType);
    
    /// <summary>JOIN 種別の制約検証</summary>
    ConstraintResult CheckJoin(JoinType joinType);
    
    /// <summary>型そのものの制約検証</summary>
    ConstraintResult CheckType(Type type);
    
    /// <summary>式全体の制約検証（複合条件）</summary>
    ConstraintResult CheckExpression(Expression expression);
    
    /// <summary>プラン位置の制約検証</summary>
    ConstraintResult CheckPlanPosition(QueryPlan plan, QueryPlanContext context);
}
```

### Flink 方言の制約（JSON：方針B）

- Flink SQL では `JSON_LENGTH` / `JSON_ARRAY_LENGTH` / `JSON_KEYS` / `JSON_OBJECT_KEYS` が存在しないため、Flink 方言では **非対応（Unsupported / fail-fast）** とする。
- JSON のパス参照は `JSON_VALUE` / `JSON_EXISTS` の範囲に限定する。
- 実装指針: `IDialectConstraints.CheckExpression(...)` で該当 API/式を検出したら `ConstraintResult.Unsupported(...)` を返す（根拠: `docs/diff_log/diff_flink_sql_json_keys_length_20251221.md`）。

### ConstraintResult

```csharp
namespace Kafka.Context.Streaming;

public record ConstraintResult(
    bool IsSupported,
    WarningLevel Level,
    string? Reason
)
{
    public static ConstraintResult Supported() 
        => new(true, WarningLevel.None, null);
    
    public static ConstraintResult Warning(string reason) 
        => new(true, WarningLevel.Warning, reason);
    
    public static ConstraintResult Unsupported(string reason) 
        => new(false, WarningLevel.Error, reason);
}

public enum WarningLevel
{
    None,
    Warning,
    Error
}
```

### QueryPlanContext

```csharp
namespace Kafka.Context.Streaming;

public record QueryPlanContext(
    bool IsAfterAggregate,
    bool IsAfterProject,
    QueryPlan? Parent
);
```

---

## 警告システム

### QueryWarning

```csharp
namespace Kafka.Context.Streaming;

public record QueryWarning(
    WarningCode Code,
    WarningLevel Level,
    string Message,
    QueryPlan? RelatedPlan
);

public enum WarningCode
{
    // 集計
    UnsupportedAggregate,
    
    // 演算子
    UnsupportedOperator,
    UnexpectedOperatorBehavior,
    
    // JOIN
    UnsupportedJoinType,
    
    // 型
    UnsupportedType,
    LimitedTypeSupport,
    
    // 式
    ComplexExpressionWarning,
    
    // プラン位置
    FilterAfterAggregate
}
```

### QueryPlanResult

```csharp
namespace Kafka.Context.Streaming;

public record QueryPlanResult(
    QueryPlan Plan,
    IReadOnlyList<QueryWarning> Warnings
);
```

---

## 方言プロバイダーインターフェース

### IStreamingDialectProvider

```csharp
namespace Kafka.Context.Streaming;

public interface IStreamingDialectProvider
{
    /// <summary>方言固有の制約</summary>
    IDialectConstraints Constraints { get; }
    
    /// <summary>Visitor</summary>
    IQueryPlanVisitor Visitor { get; }
    
    /// <summary>式から論理プランを構築</summary>
    QueryPlanResult BuildPlan(Expression expression);
    
    /// <summary>論理プランから DDL を生成</summary>
    string GenerateDdl(QueryPlan plan, string objectName, string outputTopic);
    
    /// <summary>DDL を実行</summary>
    Task ExecuteAsync(string ddl, CancellationToken ct = default);
}
```

---

## 検証の責務分離

```
┌─────────────────────────────────────────────────────────┐
│              Kafka.Context.Streaming                    │
│                                                         │
│  責務: クエリ構築                                       │
│  検証: 警告（Warning）のみ                              │
│        - IDialectConstraints で制約違反を検出           │
│        - 構築は継続                                     │
│        - 警告情報を QueryPlanResult に付与              │
│                                                         │
└─────────────────────────────────────────────────────────┘
                          ↓ QueryPlanResult (Plan + Warnings)
┌─────────────────────────────────────────────────────────┐
│           Streaming.Flink / Streaming.Ksql              │
│                                                         │
│  責務: SQL 生成 + 登録                                  │
│  検証: Fail-Fast                                        │
│        - エンジンへの登録時にエラー → 例外              │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 完全な利用例

### Context 定義

```csharp
using Kafka.Context;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;

public class OrderContext : KafkaContext
{
    public EventSet<Order> Orders { get; private set; }
    public EventSet<Customer> Customers { get; private set; }
    
    protected override void OnConfiguring(KafkaContextOptionsBuilder builder)
    {
        builder.UseStreaming<FlinkDialectProvider>();
    }
    
    protected override void OnModelCreating(IModelBuilder modelBuilder)
    {
        // 入力エンティティ
        modelBuilder.Entity<Order>();
        modelBuilder.Entity<Customer>();
        
        // 派生エンティティ（Streaming Query）
        modelBuilder.Entity<Order5m>().ToQuery(q => q
            .From<Order>()
            .Where(o => o.Status == "completed")
            .GroupBy(o => o.CustomerId)
            .Select(g => new Order5m
            {
                CustomerId = g.Key,
                TotalAmount = g.Sum(x => x.Amount),
                OrderCount = g.Count()
            })
            .Having(x => x.TotalAmount > 1000));
    }
}
```

### エンティティ定義

```csharp
// 入力エンティティ
[KafkaTopic("orders")]
public class Order
{
    public string Id { get; set; }
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

// 派生エンティティ（出力endpoint：型名規約 or KafkaTopicAttribute）
public class Order5m
{
    public string CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public int OrderCount { get; set; }
}

// 派生エンティティ（出力endpoint：型名規約）
public class CustomerStats  // → Topic: customer-stats
{
    public string CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
}
```

### 実行フロー

```csharp
// 起動時
var ctx = new OrderContext();

// Topic + Schema Registry の Provision（既存）
await ctx.ProvisionAsync();

// Streaming Query の Provision（Flink に登録）
await ctx.ProvisionStreamingAsync();

// 以降、通常の Produce / Consume
await ctx.Orders.AddAsync(new Order { ... });
```

---

## パッケージ依存関係

```
Kafka.Context.Abstractions        ← 基底インターフェース（依存なし）
    ↑
Kafka.Context.Streaming           ← 共通抽象層
    ↑
Kafka.Context.Streaming.Flink     ← Flink 方言
Kafka.Context.Streaming.Ksql      ← ksqlDB 方言
    ↑
Kafka.Context                     ← メインパッケージ
```

---

## 公開インターフェース一覧

| インターフェース/クラス | 配置 | 役割 |
|------------------------|------|------|
| `EntityModelBuilder<T>` | Abstractions | エンティティビルダー（sealed） |
| `IModelBuilder` | Abstractions | モデルビルダー IF |
| `EntityModelBuilderStreamingExtensions` | Streaming | ToQuery 拡張メソッド |
| `IStreamingQueryable<T>` | Streaming | ストリーミングクエリ基底 |
| `IStreamingOrderedQueryable<T>` | Streaming | ソート済みクエリ |
| `IStreamingGroupedQueryable<TKey, T>` | Streaming | グループ化済みクエリ |
| `IStreamingGrouping<TKey, T>` | Streaming | グループ要素 |
| `IStreamingQueryBuilder` | Streaming | クエリビルダー IF |
| `IStreamingDialectProvider` | Streaming | 方言プロバイダー IF |
| `IDialectConstraints` | Streaming | 方言制約 IF |
| `IQueryPlanVisitor` | Streaming | プラン Visitor IF |
| `StreamingQueryableExtensions` | Streaming | LINQ 拡張メソッド |
| `StreamingGroupingExtensions` | Streaming | 集計関数拡張メソッド |
| `StreamingQueryRegistry` | Streaming | Query 定義格納 |
| `StreamingQueryDefinition` | Streaming | クエリ定義 |
| `QueryPlan` | Streaming | 論理プラン基底 |
| `SourcePlan` | Streaming | ソーストピック参照 |
| `FilterPlan` | Streaming | WHERE 条件 |
| `ProjectPlan` | Streaming | SELECT 射影 |
| `DistinctPlan` | Streaming | DISTINCT |
| `AggregatePlan` | Streaming | GROUP BY + 集計 |
| `HavingPlan` | Streaming | HAVING 条件 |
| `OrderPlan` | Streaming | ORDER BY |
| `JoinPlan` | Streaming | JOIN |
| `QueryPlanResult` | Streaming | 構築結果 + 警告 |
| `QueryPlanContext` | Streaming | プラン位置コンテキスト |
| `QueryWarning` | Streaming | 警告情報 |
| `ConstraintResult` | Streaming | 制約検証結果 |
| `AggregateFunction` | Streaming | 集計関数定義 |
| `OrderElement` | Streaming | ソート要素定義 |
| `AggregateFunctionType` | Streaming | 集計関数種別 |
| `OperatorType` | Streaming | 演算子種別 |
| `JoinType` | Streaming | JOIN 種別 |
| `WarningLevel` | Streaming | 警告レベル |
| `WarningCode` | Streaming | 警告コード |
| `KafkaContextOptionsBuilder` | Context | オプションビルダー |
| `KafkaContextOptions` | Context | オプション |
| `NamingConvention` | Context | 命名規約ヘルパー |
| `KafkaTopicAttribute` | Context | Topic 名属性 |
| `SchemaSubjectAttribute` | Context | Subject 名属性 |
| `DeadLetterTopicAttribute` | Context | DLQ 名属性 |

---

## 設計原則（確定）

1. **責務の明確化** — クエリ構築のみを担当し、SQL 生成・実行は方言層に委譲
2. **方言の強みを殺さない** — Window / Limit 等のセマンティクス差異が大きい機能は共通化しない
3. **SQL 標準ベース** — 集計関数は SQL 標準を基準に共通定義
4. **オーバーライド可能** — 方言固有の制約は IDialectConstraints で表現
5. **警告と Fail-Fast の分離** — 共通層は警告のみ、Fail-Fast は方言層の責務
6. **明示的な API** — Where / Having は自動判定せず明示的に使い分け
7. **標準 LINQ 互換** — Join API は ResultSelector を含め、内部で分離
8. **Context は方言に依存** — OnConfiguring で方言を決定、途中変更なし
9. **Visitor で解釈** — ToQuery は共通、方言 Visitor が SQL 生成
10. **冪等な Provision** — ロールバックなし、再実行で復旧可能
11. **属性 + 規約** — 明示指定優先、未指定時は命名規約でフォールバック
12. **拡張メソッド配置** — ToQuery は Streaming 層に配置、依存方向を正しく保つ
13. **利用者責任** — readOnly / writeOnly は採用せず、派生エンティティへの書き込みは利用者責任
14. **Provision 完全分離** — ProvisionAsync と ProvisionStreamingAsync は独立

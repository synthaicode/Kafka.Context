# Kafka.Context.Streaming 設計計画

## 概要

Kafka.Context.Streaming は、LINQ 式をストリーム処理 SQL に変換する共通抽象層。バックエンド（Flink / ksqlDB）に依存しない統一インターフェースを提供する。

## 設計原則

### 薄いラッパー（Dapper 方式）

- 共通範囲は最小限に保つ
- 複雑な機能はバックエンド固有に委譲
- 迷ったら固有側に倒す

### 共通化の判断基準

| 基準 | 判定 |
|------|------|
| 両方で同じ意味・同じ結果 | → 共通 |
| 方言差が吸収可能 | → 共通 |
| 方言差が意味を変える | → 固有 |
| 一方にしかない機能 | → 固有 |

## パッケージ構成

```
Kafka.Context.Streaming           ← 共通抽象（LINQ 変換基盤）
Kafka.Context.Streaming.Flink     ← Flink SQL 方言 + Gateway クライアント
Kafka.Context.Streaming.Ksql      ← ksqlDB 方言（将来、Kafka.Ksql.Linq から移行）
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

## 共通 API（Kafka.Context.Streaming）

### フィルタ・射影

```csharp
IStreamingQueryable<T> Where(Expression<Func<T, bool>> predicate);
IStreamingQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
```

### グループ化

```csharp
IStreamingGrouping<TKey, T> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector);
```

### 集計（基本）

```csharp
// IStreamingGrouping<TKey, T> から
long Count();
TResult Sum<TResult>(Expression<Func<T, TResult>> selector);
TResult Min<TResult>(Expression<Func<T, TResult>> selector);
TResult Max<TResult>(Expression<Func<T, TResult>> selector);
double Average(Expression<Func<T, decimal>> selector);
```

### JOIN（2 テーブル限定）

```csharp
IStreamingQueryable<TResult> Join<TOther, TKey, TResult>(
    IStreamingQueryable<TOther> other,
    Expression<Func<T, TKey>> leftKeySelector,
    Expression<Func<TOther, TKey>> rightKeySelector,
    Expression<Func<T, TOther, TResult>> resultSelector);

IStreamingQueryable<TResult> LeftJoin<TOther, TKey, TResult>(
    IStreamingQueryable<TOther> other,
    Expression<Func<T, TKey>> leftKeySelector,
    Expression<Func<TOther, TKey>> rightKeySelector,
    Expression<Func<T, TOther, TResult>> resultSelector);
```

### ウィンドウ（基本形）

```csharp
IWindowedStreamingQueryable<T> TumblingWindow(TimeSpan size);
IWindowedStreamingQueryable<T> HoppingWindow(TimeSpan size, TimeSpan hop);
IWindowedStreamingQueryable<T> SessionWindow(TimeSpan gap);
```

### 実行

```csharp
Task<IStreamingQueryHandle> CreatePersistentQueryAsync(CancellationToken ct = default);
IAsyncEnumerable<T> ToAsyncEnumerable(CancellationToken ct = default);
```

### クエリハンドル

```csharp
public interface IStreamingQueryHandle
{
    string QueryId { get; }
    Task<QueryStatus> GetStatusAsync(CancellationToken ct = default);
    Task WaitForRunningAsync(TimeSpan timeout, CancellationToken ct = default);
    Task CancelAsync(CancellationToken ct = default);
}

public enum QueryStatus
{
    Pending,
    Running,
    Finished,
    Failed,
    Canceled
}
```

## Flink 固有 API（Kafka.Context.Streaming.Flink）

```csharp
namespace Kafka.Context.Streaming.Flink;

public static class FlinkStreamingExtensions
{
    // 3 テーブル以上の JOIN
    public static IFlinkJoinBuilder<T> Join<T>(this IStreamingQueryable<T> source);
    
    // 時間セマンティクス
    public static IStreamingQueryable<T> WithWatermark<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, DateTime>> timestampSelector,
        TimeSpan maxOutOfOrderness);
    
    public static IStreamingQueryable<T> ProcessingTime<T>(this IStreamingQueryable<T> source);
    public static IStreamingQueryable<T> EventTime<T>(this IStreamingQueryable<T> source);
    
    // Flink 固有 JOIN
    public static IStreamingQueryable<TResult> IntervalJoin<T, TOther, TResult>(...);
    public static IStreamingQueryable<TResult> TemporalJoin<T, TOther, TResult>(...);
}
```

## ksqlDB 固有 API（Kafka.Context.Streaming.Ksql）

```csharp
namespace Kafka.Context.Streaming.Ksql;

public static class KsqlStreamingExtensions
{
    // 出力モード
    public static IStreamingQueryable<T> Emit<T>(
        this IStreamingQueryable<T> source, 
        EmitMode mode);
    
    // タイムスタンプ指定
    public static IStreamingQueryable<T> WithTimestamp<T>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, DateTime>> timestampSelector);
    
    // パーティション
    public static IStreamingQueryable<T> PartitionBy<T, TKey>(
        this IStreamingQueryable<T> source,
        Expression<Func<T, TKey>> keySelector);
}

public enum EmitMode
{
    Changes,
    Final
}
```

## DDL 生成

### 共通インターフェース

```csharp
public interface IStreamingModelBuilder
{
    void Entity<T>(Action<IStreamingEntityBuilder<T>> configure);
}

public interface IStreamingEntityBuilder<T>
{
    void ToStream(string streamName);
    void ToTable(string tableName);
    void HasKey<TKey>(Expression<Func<T, TKey>> keySelector);
}
```

### Provisioning

```csharp
// デフォルト: IF NOT EXISTS（冪等）
await ctx.ProvisionStreamingAsync();

// 明示的に OR REPLACE（Flink のみ）
await ctx.ProvisionStreamingAsync(options =>
{
    options.Streams["orders"].Mode = CreateMode.OrReplace;
});

public enum CreateMode
{
    IfNotExists,  // デフォルト
    OrReplace     // Flink のみ
}
```

## 使用例

### 基本的なクエリ

```csharp
// Flink を使う場合
services.AddKafkaStreaming(options => options.UseFlink(flinkConfig));

// クエリ（バックエンド非依存）
var handle = await ctx.Orders
    .Where(o => o.Amount > 1000)
    .GroupBy(o => o.CustomerId)
    .TumblingWindow(TimeSpan.FromMinutes(5))
    .Select(g => new OrderSummary 
    { 
        CustomerId = g.Key, 
        TotalAmount = g.Sum(o => o.Amount),
        Count = g.Count()
    })
    .CreatePersistentQueryAsync();

await handle.WaitForRunningAsync(TimeSpan.FromSeconds(30));
```

### バックエンド固有機能

```csharp
// Flink 固有の watermark 設定
await ctx.Orders
    .AsFlink()
    .WithWatermark(o => o.EventTime, TimeSpan.FromSeconds(5))
    .Where(o => o.Amount > 1000)
    .CreatePersistentQueryAsync();
```

## 実装フェーズ

### Phase 1: 基盤（Flink 優先）

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

## 責務外（Non-Goals）

- 3 テーブル以上の JOIN（共通では対応しない）
- バックエンド固有の高度な時間処理（固有 API で対応）
- 状態管理の抽象化（バックエンドに委譲）
- Dashboard / 監視機能（Datadog 等に委譲）

## Kafka.Ksql.Linq との関係

```
現状:
  Kafka.Ksql.Linq → 独立したライブラリ（30k steps）

将来:
  Kafka.Context.Streaming.Ksql → 式変換ロジックを移植
  Kafka.Ksql.Linq → メンテナンスモード or 非推奨化
```

移行は段階的に行い、既存ユーザーへの影響を最小化する。

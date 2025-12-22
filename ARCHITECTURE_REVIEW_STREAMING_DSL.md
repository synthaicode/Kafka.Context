# アーキテクチャレビュー: Streaming DSL境界設定

**レビュー日**: 2025-12-22
**対象ブランチ**: release/1.2.0
**レビュー対象**: Streaming名前空間とFlink実装の境界設定、ksqlDB拡張の妥当性

---

## エグゼクティブサマリー

release/1.2.0の実装は、**非常に優れた境界設定**を実現しています。Streaming名前空間は完全にエンジン非依存の抽象化層として設計されており、Flink固有の実装は明確に分離されています。この設計により、ksqlDBへの拡張は**十分に実現可能**であり、推奨されるアーキテクチャパターンに従って実装できます。

### 主要評価結果

- ✅ **境界設定**: 優秀 - 明確なインターフェース分離
- ✅ **拡張性**: 優秀 - ksqlDB実装可能
- ✅ **依存関係管理**: 優秀 - クリーンな依存方向
- ⚠️ **改善余地**: あり - 関数レジストリの共有化検討

---

## 1. 現在の実装分析

### 1.1 Streaming名前空間（Kafka.Context.Streaming）

**役割**: エンジン非依存のDSL抽象化層

#### コア抽象化

```
📦 Kafka.Context.Streaming (32ファイル)
├── IStreamingQueryable<T>           # クエリ可能インターフェース
├── StreamingQueryPlan               # エンジン非依存のクエリプラン
│   ├── SourceTypes                  # ソース型
│   ├── JoinPredicates               # JOIN条件
│   ├── WherePredicates              # WHERE条件
│   ├── SelectSelector               # SELECT射影
│   ├── GroupByClause                # GROUP BY句
│   ├── Window                       # ウィンドウ仕様
│   └── SinkMode                     # シンクモード
├── StreamingQueryBuilder            # DSLビルダー
├── StreamingWindowSpec              # ウィンドウ仕様（Tumble/Hop/Session）
└── Provider Interfaces
    ├── IStreamingDialectProvider    # エンジン固有のSQL生成
    └── IStreamingCatalogDdlProvider # DDL生成
```

#### 重要な設計特性

1. **エンジン非依存**: すべての構造がエンジン中立
2. **LINQ互換**: C# Expressionベースのクエリ構築
3. **宣言的**: クエリプランは実行ロジックを含まない
4. **拡張可能**: プロバイダーインターフェースによる拡張ポイント

**依存関係**:
- ✅ Kafka.Context.Abstractions のみ
- ✅ 外部パッケージ依存なし

---

### 1.2 Flink名前空間（Kafka.Context.Streaming.Flink）

**役割**: Flink SQL固有の実装

```
📦 Kafka.Context.Streaming.Flink (17ファイル)
├── FlinkDialectProvider              # IStreamingDialectProvider実装
│   ├── NormalizeObjectName()        # オブジェクト名正規化
│   ├── GenerateDdl()                # Flink SQL生成
│   └── GenerateSourceDdls()         # Kafkaコネクタ定義生成
├── FlinkSqlRenderer                  # StreamingQueryPlan → Flink SQL
│   ├── RenderSelect()               # SELECT文生成
│   ├── RenderFrom()                 # FROM句（TVF対応）
│   └── RenderInterval()             # INTERVAL式
├── FlinkExpressionVisitor            # C# Expression → Flink SQL
│   ├── RenderBinary()               # 二項演算子
│   ├── RenderCall()                 # メソッド呼び出し
│   └── RenderMemberAccess()         # メンバーアクセス
├── FlinkFunctionRegistry             # Flink関数マッピング
│   ├── RenderFlinkSqlCall()         # FlinkSql.* メソッド
│   ├── RenderWindowCall()           # FlinkWindow.* メソッド
│   ├── RenderAggCall()              # FlinkAgg.* メソッド
│   └── RenderStringCall()           # string.* メソッド
└── DSL Helpers
    ├── FlinkSql                      # Flink固有関数（60+メソッド）
    ├── FlinkWindow                   # window_start/end/proctime
    ├── FlinkAgg                      # COUNT/SUM/AVG
    └── FlinkModelBuilderExtensions   # .FlinkSource<T>()
```

#### Flink固有要素

1. **関数マッピング**:
   - `FlinkSql.Concat()` → `CONCAT(...)`
   - `FlinkWindow.Start()` → `window_start`
   - `string.Contains()` → `LIKE '%...%'`

2. **ウィンドウTVF**:
   - `TUMBLE(TABLE tbl, DESCRIPTOR(ts), INTERVAL '5' MINUTE)`
   - `HOP(...)` / `SESSION(...)`

3. **Kafkaコネクタ**:
   - `connector=kafka`, `format=confluent-avro-registry`
   - Schema Registry統合

**依存関係**:
- ✅ Kafka.Context.Streaming
- ✅ Kafka.Context.Abstractions
- ✅ Kafka.Context.Application

---

## 2. 境界設定の妥当性評価

### 2.1 インターフェース設計 ⭐⭐⭐⭐⭐

#### IStreamingDialectProvider

```csharp
public interface IStreamingDialectProvider
{
    string NormalizeObjectName(string suggestedObjectName);
    string GenerateDdl(StreamingQueryPlan plan,
                      StreamingStatementKind kind,
                      StreamingOutputMode outputMode,
                      string objectName,
                      string outputTopic);
    Task ExecuteAsync(string ddl, CancellationToken cancellationToken);
}
```

**評価**:
- ✅ **エンジン非依存**: StreamingQueryPlanのみに依存
- ✅ **責任明確**: オブジェクト名正規化、DDL生成、実行の3つの責務
- ✅ **テスタビリティ**: モックによるテストが容易

#### IStreamingCatalogDdlProvider

```csharp
public interface IStreamingCatalogDdlProvider
{
    IReadOnlyList<string> GenerateSourceDdls(
        IReadOnlyList<StreamingSourceDefinition> sources);
    IReadOnlyList<string> GenerateSinkDdls(
        IReadOnlyList<StreamingSinkDefinition> sinks);
}
```

**評価**:
- ✅ **エンジン非依存**: カタログ定義のみに依存
- ✅ **拡張性**: 各エンジンのコネクタ要件に対応可能

### 2.2 依存関係方向 ⭐⭐⭐⭐⭐

```
┌─────────────────────────────────────┐
│   Kafka.Context.Application        │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│   Kafka.Context.Abstractions        │◄───────┐
└────────────┬────────────────────────┘        │
             │                                  │
             ▼                                  │
┌─────────────────────────────────────┐        │
│   Kafka.Context.Streaming           │        │
│   (エンジン非依存DSL抽象化)          │        │
└────────────┬────────────────────────┘        │
             │                                  │
             ▼                                  │
┌─────────────────────────────────────┐        │
│   Kafka.Context.Streaming.Flink     │────────┘
│   (Flink固有実装)                    │
└─────────────────────────────────────┘
```

**評価**:
- ✅ **単方向依存**: Flink → Streaming（逆依存なし）
- ✅ **循環依存なし**: クリーンなアーキテクチャ
- ✅ **置換可能**: Flinkを削除しても Streaming は影響なし

### 2.3 拡張ポイント ⭐⭐⭐⭐⭐

| 拡張ポイント | 実装方法 | エンジン固有度 |
|-------------|---------|--------------|
| **SQL方言** | `IStreamingDialectProvider` | 高 |
| **DDL生成** | `IStreamingCatalogDdlProvider` | 高 |
| **関数マッピング** | `FunctionRegistry` (Flink実装) | 高 |
| **型マッピング** | `MapClrToFlinkType()` | 高 |
| **DSLヘルパー** | `FlinkSql`/`FlinkWindow`/`FlinkAgg` | 高 |
| **設定拡張** | `FlinkModelBuilderExtensions` | 高 |

**評価**:
- ✅ すべての拡張ポイントが明確に定義
- ✅ エンジン固有実装は分離されたアセンブリに隔離

---

## 3. ksqlDB拡張の妥当性評価

### 3.1 必要な実装コンポーネント

ksqlDB拡張には、Flinkと並行して以下を実装：

```
📦 Kafka.Context.Streaming.KsqlDb (新規)
├── KsqlDbDialectProvider              # IStreamingDialectProvider
├── KsqlDbSqlRenderer                  # StreamingQueryPlan → ksqlDB SQL
├── KsqlDbExpressionVisitor            # C# Expression → ksqlDB SQL
├── KsqlDbFunctionRegistry             # ksqlDB関数マッピング
└── DSL Helpers
    ├── KsqlDbSql                      # ksqlDB固有関数
    ├── KsqlDbWindow                   # WINDOWSTART/WINDOWEND
    └── KsqlDbAgg                      # COLLECT_LIST/HISTOGRAM等
```

### 3.2 主要な差異ポイント

#### 3.2.1 ウィンドウ構文

| エンジン | 構文 |
|---------|------|
| **Flink** | `TABLE(TUMBLE(TABLE tbl, DESCRIPTOR(ts), INTERVAL '5' MINUTE))` |
| **ksqlDB** | `SELECT ... FROM tbl WINDOW TUMBLING (SIZE 5 MINUTES)` |

**実装**: `KsqlDbSqlRenderer.RenderFrom()` でウィンドウ句を生成

#### 3.2.2 集約関数

| 機能 | Flink | ksqlDB |
|-----|-------|--------|
| カウント | `COUNT(*)` | `COUNT(*)` ✅ |
| 最新値 | ❌ | `LATEST_BY_OFFSET(val)` ✅ |
| 配列収集 | ❌ | `COLLECT_LIST(val)` ✅ |
| ヒストグラム | ❌ | `HISTOGRAM(val)` ✅ |

**実装**: `KsqlDbAgg` クラスで固有関数を提供

#### 3.2.3 文字列関数

現在FlinkSqlクラスには以下のksqlDB用スタブが存在：

```csharp:src/Kafka.Context.Streaming.Flink/Streaming/Flink/FlinkSql.cs
public static object KsqlInstr(string input, string needle)
    => throw new NotSupportedException("Use in ToQuery only.");
public static object KsqlLen(string input)
    => throw new NotSupportedException("Use in ToQuery only.");
// ... 他8関数
```

**推奨**: これらをKsqlDbSqlクラスに移動

#### 3.2.4 CREATE文の違い

| エンジン | 構文 |
|---------|------|
| **Flink** | `CREATE TABLE tbl (...) WITH ('connector'='kafka', ...)` |
| **ksqlDB** | `CREATE STREAM tbl (...) WITH (KAFKA_TOPIC='...', VALUE_FORMAT='AVRO', ...)` |

**実装**: `KsqlDbDialectProvider.GenerateSourceDdls()` で実装

### 3.3 共有可能なコンポーネント ⭐⭐⭐⭐

以下は既にエンジン非依存で実装されており、そのまま再利用可能：

- ✅ `StreamingQueryPlan` - 完全共有
- ✅ `StreamingQueryBuilder` - 完全共有
- ✅ `StreamingWindowSpec` - 完全共有
- ✅ `StreamingPredicateBuilder` - 完全共有
- ✅ `StreamingGroupByClauseBuilder` - 完全共有
- ⚠️ `ExpressionVisitor` - 基本ロジック共有可能（型マッピングは個別）

**推奨**: ExpressionVisitorの基底クラス化を検討

---

## 4. 改善提案

### 4.1 関数レジストリの抽象化 [優先度: 中]

**現状**: `FlinkFunctionRegistry` はFlink固有実装

**提案**: 共通基底クラスの導入

```csharp
// Kafka.Context.Streaming (共通層)
public abstract class StreamingFunctionRegistry
{
    public abstract bool TryRender(
        MethodCallExpression call,
        Func<Expression, string> render,
        Dictionary<ParameterExpression, string> paramAliases,
        out string sql);

    // 共通実装: string.*, Math.* の基本マッピング
    protected virtual string RenderStringCall(
        MethodCallExpression call,
        Func<Expression, string> render)
    {
        // 共通ロジック: ToUpper() → UPPER(), Trim() → TRIM()
    }
}

// Kafka.Context.Streaming.Flink
internal sealed class FlinkFunctionRegistry : StreamingFunctionRegistry
{
    protected override string RenderStringCall(...)
    {
        // Flink固有の上書き（必要な場合のみ）
        base.RenderStringCall(...);
    }

    // Flink固有: FlinkSql.*, FlinkWindow.*, FlinkAgg.*
}

// Kafka.Context.Streaming.KsqlDb
internal sealed class KsqlDbFunctionRegistry : StreamingFunctionRegistry
{
    // ksqlDB固有: KsqlDbSql.*, KsqlDbWindow.*, KsqlDbAgg.*
}
```

**利点**:
- ✅ 重複コード削減（string/Math関数マッピング）
- ✅ 保守性向上
- ✅ 一貫性の確保

### 4.2 型マッピング抽象化 [優先度: 中]

**現状**: `MapClrToFlinkType()` はFlinkDialectProvider内

**提案**: インターフェース化

```csharp
public interface IStreamingTypeMapper
{
    string MapClrToSqlType(
        Type clrType,
        PropertyInfo propertyInfo,
        bool isSink,
        StreamingEventTimeConfig? eventTime);
}
```

**実装**:
- `FlinkTypeMapper`: `TIMESTAMP(3)`, `STRING`, `DECIMAL(38,18)`
- `KsqlDbTypeMapper`: `TIMESTAMP`, `VARCHAR`, `DECIMAL`

### 4.3 DSLヘルパー名前空間の整理 [優先度: 低]

**現状**: `FlinkSql` クラスに ksqlDB関数のスタブが混在

```csharp:src/Kafka.Context.Streaming.Flink/Streaming/Flink/FlinkSql.cs
// Flink関数
public static string Concat(params object[] parts) => ...;
public static string JsonValue(string json, string path) => ...;

// ksqlDB関数（現在はFlinkSqlクラス内）
public static object KsqlInstr(string input, string needle) => ...;
public static object KsqlLen(string input) => ...;
```

**提案**: 名前空間を明確に分離

```
Kafka.Context.Streaming.Flink
├── FlinkSql      # Flink専用関数のみ
├── FlinkWindow
└── FlinkAgg

Kafka.Context.Streaming.KsqlDb (新規)
├── KsqlDbSql     # ksqlDB専用関数（Ksqlプレフィックス除外）
├── KsqlDbWindow
└── KsqlDbAgg
```

**移行方法**:
1. `Kafka.Context.Streaming.KsqlDb.KsqlDbSql` クラスを新規作成
2. `Ksql*` メソッドを移動
3. `FlinkSql` からは `[Obsolete]` でマーク → 次バージョンで削除

### 4.4 設定ビルダーの統一 [優先度: 低]

**現状**: `FlinkModelBuilderExtensions.FlinkSource<T>()`

**提案**: エンジン中立の設定パターン

```csharp
// エンジン中立の共通設定
modelBuilder.Entity<T>()
    .StreamingSource(source => source
        .WithEventTime(...)
        .WithWatermark(...));

// エンジン固有の設定（必要に応じて）
modelBuilder.Entity<T>()
    .FlinkSource(source => source
        .WithProctimeColumn());
```

---

## 5. ksqlDB実装ロードマップ

### Phase 1: 基本実装 [2-3週間]

1. **アセンブリ作成**
   - `Kafka.Context.Streaming.KsqlDb.csproj`
   - 依存: Streaming, Abstractions

2. **プロバイダー実装**
   - `KsqlDbDialectProvider` (IStreamingDialectProvider)
   - `KsqlDbSqlRenderer` (StreamingQueryPlan → ksqlDB SQL)

3. **基本関数マッピング**
   - `KsqlDbExpressionVisitor`
   - `KsqlDbFunctionRegistry`
   - string/Math基本関数

4. **ユニットテスト**
   - SQL生成のテスト
   - 関数マッピングのテスト

### Phase 2: 高度な機能 [1-2週間]

1. **ウィンドウ対応**
   - TUMBLING/HOPPING/SESSION
   - WINDOWSTART/WINDOWEND

2. **ksqlDB固有集約**
   - COLLECT_LIST/COLLECT_SET
   - HISTOGRAM
   - TOPK/TOPKDISTINCT

3. **DSLヘルパー**
   - `KsqlDbSql` クラス
   - `KsqlDbWindow` クラス
   - `KsqlDbAgg` クラス

### Phase 3: 統合とテスト [1週間]

1. **物理テスト**
   - ksqlDB実環境でのテスト
   - Flink/ksqlDB並行テスト

2. **ドキュメント**
   - README更新
   - 移行ガイド

3. **サンプル追加**
   - `examples/streaming-ksqldb`

---

## 6. 結論と推奨事項

### 6.1 境界設定の評価: ⭐⭐⭐⭐⭐ (優秀)

現在の実装は**非常に優れた境界設定**を実現しています：

1. ✅ **完全なエンジン非依存性**: Streaming名前空間は一切のエンジン固有コードを含まない
2. ✅ **明確なインターフェース**: プロバイダーパターンによる拡張ポイント
3. ✅ **クリーンな依存方向**: 循環依存なし、単方向依存
4. ✅ **高い凝集度**: 各コンポーネントが単一責任

### 6.2 ksqlDB拡張の妥当性: ⭐⭐⭐⭐⭐ (高度に実現可能)

ksqlDB拡張は**推奨されるアーキテクチャパターン**に完全に適合：

1. ✅ **既存コードへの影響なし**: 新規アセンブリとして実装
2. ✅ **共通抽象化の再利用**: StreamingQueryPlanをそのまま使用
3. ✅ **並行開発可能**: Flink実装と独立して開発
4. ✅ **テスト独立性**: エンジン別のテストスイート

### 6.3 優先推奨事項

#### 即座に実施すべき（ksqlDB実装前）

1. **ksqlDB関数スタブの移動**
   - `FlinkSql.Ksql*()` メソッドをobsoleteマーク
   - マイグレーションパスの提供

#### ksqlDB実装と並行して検討

2. **関数レジストリの基底クラス化**
   - 重複削減
   - 保守性向上

3. **型マッピングのインターフェース化**
   - 拡張性向上

#### 将来的に検討（v1.3.0以降）

4. **設定ビルダーの統一**
   - ユーザーエクスペリエンス向上

---

## 7. 参考: 主要ファイルパス

```
src/Kafka.Context.Streaming/Streaming/
├── IStreamingDialectProvider.cs              # L6-L18
├── IStreamingCatalogDdlProvider.cs           # L3-L7
├── StreamingQueryPlan.cs                     # L7-L43
├── StreamingQueryBuilder.cs                  # L8-L128
└── StreamingWindowSpec.cs                    # L5-L10

src/Kafka.Context.Streaming.Flink/Streaming/Flink/
├── FlinkDialectProvider.cs                   # L9-L377
├── FlinkSqlRenderer.cs                       # L11-L235
├── FlinkExpressionVisitor.cs                 # L10-L132
├── FlinkFunctionRegistry.cs                  # L10-L351
├── FlinkSql.cs                               # L3-L84
├── FlinkWindow.cs                            # L3-L10
└── FlinkAgg.cs                               # L3-L18

examples/streaming-flink/Program.cs            # L11-L100
```

---

**レビュアー署名**: Claude (Sonnet 4.5)
**承認ステータス**: ✅ 境界設定は妥当、ksqlDB拡張推奨

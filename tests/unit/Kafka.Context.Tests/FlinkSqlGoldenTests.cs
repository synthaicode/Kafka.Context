using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Kafka.Context.Streaming;
using Kafka.Context.Streaming.Flink;

namespace Kafka.Context.Tests;

[Trait("Level", "L4")]
public sealed class FlinkSqlGoldenTests
{
    private static readonly string GoldenRoot =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Golden", "FlinkSql");

    private static readonly bool UpdateGolden =
        string.Equals(Environment.GetEnvironmentVariable("KAFKA_CONTEXT_GOLDEN_UPDATE"), "1", StringComparison.Ordinal);

    [Fact]
    public void FlinkSql_Golden_Ddl_IsStable()
    {
        foreach (var testCase in Cases())
        {
            if (testCase.ExpectFailure)
            {
                Assert.Throws<NotSupportedException>(() => RenderCase(testCase));
                continue;
            }

            var ddl = RenderCase(testCase);
            var path = Path.Combine(GoldenRoot, $"{testCase.Name}.ddl");

            if (UpdateGolden)
            {
                Directory.CreateDirectory(GoldenRoot);
                File.WriteAllText(path, ddl);
                continue;
            }

            Assert.True(File.Exists(path), $"Golden file missing: {path}");
            var expected = File.ReadAllText(path);
            Assert.Equal(NormalizeNewlines(expected), NormalizeNewlines(ddl));
        }
    }

    private static string RenderCase(FlinkGoldenCase testCase)
    {
        var builder = new StreamingQueryBuilder();
        var queryable = testCase.Build(builder);
        var plan = queryable.Plan with { SourceTopics = testCase.SourceTopics };
        var kind = DetermineKind(plan);
        var dialect = new FlinkDialectProvider((_, _) => Task.CompletedTask);

        return dialect.GenerateDdl(
            plan,
            kind,
            StreamingOutputMode.Changelog,
            objectName: testCase.Name,
            outputTopic: testCase.Name);
    }

    private static StreamingStatementKind DetermineKind(StreamingQueryPlan plan)
    {
        if (plan.Window is not null || plan.HasGroupBy || plan.HasAggregate || plan.HasHaving)
            return StreamingStatementKind.TableCtas;

        return StreamingStatementKind.StreamInsert;
    }

    private static IEnumerable<FlinkGoldenCase> Cases()
    {
        yield return Ok("01_basic_select", b => b.From<Order>()
            .Where(o => o.Amount >= 100)
            .Select(o => new { o.OrderId, o.CustomerId, o.Amount }),
            "orders");

        yield return Ok("02_join", b => b.From<Order>()
            .Join<Order, Customer>((o, c) => o.CustomerId == c.CustomerId)
            .Select((o, c) => new { o.OrderId, o.CustomerId, c.Name, o.Amount }),
            "orders", "customers");

        yield return Ok("03_groupby", b => b.From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new { o.CustomerId, Cnt = FlinkAgg.Count(), SumAmount = FlinkAgg.Sum(o.Amount) }),
            "orders");

        yield return Ok("04_having", b => b.From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new { o.CustomerId, Cnt = FlinkAgg.Count() })
            .Having(o => o.Cnt >= 5),
            "orders");

        yield return Ok("05_window_tumble", b => b.From<OrderWithTime>()
            .TumbleWindow(o => o.EventTime, TimeSpan.FromSeconds(5))
            .GroupBy(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End(), Cnt = FlinkAgg.Count() }),
            "orders");

        yield return Ok("10_string_flink_ok", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Substring_2_3 = "abcdef".Substring(1, 3),
                Trimmed = "  a b  ".Trim(),
                Uppered = "Abc".ToUpper(),
                Lowered = "AbC".ToLower(),
                CharLength = "abc".Length,
                Pos = FlinkSql.Position("cd", "abcdef"),
                Loc = FlinkSql.Locate("cd", "abcdef"),
                Concatenated = FlinkSql.Concat("a", "-", "b"),
                Replaced = "a-b-c".Replace("-", "_"),
                RegexReplaced = FlinkSql.RegexpReplace("a--b", "-+", "-"),
            }),
            "probe");

        yield return Ok("11_datetime_extract_flink_ok", b => b.From<ProbeRow>()
            .Select(x => new
            {
                YearPart = x.EventTime.Year,
                MonthPart = x.EventTime.Month,
                DayPart = x.EventTime.Day,
                HourPart = x.EventTime.Hour,
                MinutePart = x.EventTime.Minute,
                SecondPart = x.EventTime.Second,
            }),
            "probe");

        yield return Ok("12_datetime_format_flink_ok", b => b.From<ProbeRow>()
            .Select(x => new
            {
                DateFormat = FlinkSql.DateFormat(x.EventTime, "yyyy-MM-dd HH:mm:ss"),
                AddMinutes = FlinkSql.TimestampAdd(FlinkTimeUnit.Minute, 5, x.EventTime),
                AddDays = FlinkSql.TimestampAdd(FlinkTimeUnit.Day, 1, x.EventTime),
            }),
            "probe");

        yield return Ok("13_cast_decimal_flink_ok", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                CastInt = FlinkSql.Cast<int>("123", FlinkSqlType.Int),
                CastBigInt = FlinkSql.Cast<long>("1234567890123", FlinkSqlType.BigInt),
                CastDecimal = FlinkSql.Cast<decimal>("1.23", FlinkSqlType.Decimal(10, 2)),
                CastDouble = FlinkSql.Cast<double>(1.2, FlinkSqlType.Double),
                CastBool = FlinkSql.Cast<bool>(true, FlinkSqlType.Boolean),
                CastNullString = FlinkSql.Cast<string>(null!, FlinkSqlType.String),
            }),
            "probe");

        yield return Ok("14_json_flink_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                A = FlinkSql.JsonValue("{\"a\":1,\"b\":{\"c\":\"x\"}}", "$.a"),
                BC = FlinkSql.JsonValue("{\"a\":1,\"b\":{\"c\":\"x\"}}", "$.b.c"),
            }),
            "probe");

        yield return Ok("15_split_lpad_rpad_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Lpad1 = FlinkSql.Lpad("7", 3, "0"),
                Rpad1 = FlinkSql.Rpad("7", 3, "0"),
            }),
            "probe");

        yield return Ok("16_split_index_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                First0 = FlinkSql.SplitIndex("a,b,c", ",", 0),
                Second1 = FlinkSql.SplitIndex("a,b,c", ",", 1),
            }),
            "probe");

        yield return Ok("17_date_parts_functions_probe", b => b.From<ProbeRow>()
            .Select(x => new
            {
                Dow = FlinkSql.DayOfWeek(x.EventTime),
                Doy = FlinkSql.DayOfYear(x.EventTime),
            }),
            "probe");

        yield return Ok("18_weekofyear_probe", b => b.From<ProbeRow>()
            .Select(x => new
            {
                WeekExtract = FlinkSql.WeekOfYearExtract(x.EventTime),
                WeekFormat = FlinkSql.WeekOfYearFormat(x.EventTime),
            }),
            "probe");

        yield return Ok("19_nested_aggregate_wrappers", b => b.From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new
            {
                o.CustomerId,
                AvgRounded1 = Math.Round(FlinkAgg.Avg(o.Amount), 1),
                AvgEmulated1 = Math.Round(
                    FlinkAgg.Sum(o.Amount) / FlinkSql.Cast<double>(FlinkAgg.Count(), FlinkSqlType.Double),
                    1),
            }),
            "orders");

        yield return Ok("20_decimal_ops_agg_probe", b => b.From<Price>()
            .GroupBy(p => p.Symbol)
            .Select(p => new
            {
                p.Symbol,
                AvgPx = Math.Round(
                    FlinkAgg.Avg(FlinkSql.Cast<decimal>(p.Px, FlinkSqlType.Decimal(18, 4))),
                    2),
                SumPx = FlinkAgg.Sum(FlinkSql.Cast<decimal>(p.Px, FlinkSqlType.Decimal(18, 4))),
            }),
            "prices");

        yield return Fail("21_split_fail", b => b.From<ProbeRow>()
            .Where(x => x.Name.Split(',') != null)
            .Select(x => new { x.Name }),
            "probe");

        yield return Ok("22_contains_startswith_endswith", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                LikeContains = "abcdef".Contains("cd"),
                LikeStartsWith = "abcdef".StartsWith("ab"),
                LikeEndsWith = "abcdef".EndsWith("ef"),
                PositionContains = "abcdef".IndexOf("cd") >= 0,
                SubstringStartsWith = "abcdef".Substring(0, 2) == "ab",
                SubstringEndsWith = "abcdef".Substring(4, 2) == "ef",
            }),
            "probe");

        yield return Ok("23_regex_like_extract", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Extract1 = FlinkSql.RegexpExtract("abc-123", "([a-z]+)-([0-9]+)", 1),
                Extract2 = FlinkSql.RegexpExtract("abc-123", "([a-z]+)-([0-9]+)", 2),
                Replaced = FlinkSql.RegexpReplace("a---b", "-+", "-"),
            }),
            "probe");

        yield return Ok("23b_regex_predicate_similar_to_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                SimilarToOk = FlinkSql.SimilarTo("abc-123", "[a-z]+-[0-9]+"),
            }),
            "probe");

        yield return Ok("24_uuid_guid_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                GuidString = FlinkSql.Cast<string>("550e8400-e29b-41d4-a716-446655440000", FlinkSqlType.String),
                UuidFn = FlinkSql.Uuid(),
            }),
            "probe");

        yield return Ok("25_timestamp_ltz_timezone_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                TsLtz = FlinkSql.ToTimestampLtz(1735689600000, 3),
                YearPart = FlinkSql.Extract(FlinkTimeUnit.Year, FlinkSql.ToTimestampLtz(1735689600000, 3)),
                TsFmt = FlinkSql.DateFormat(FlinkSql.ToTimestampLtz(1735689600000, 3), "yyyy-MM-dd HH:mm:ss"),
            }),
            "probe");

        yield return Ok("26_json_array_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                FirstVal = FlinkSql.JsonValue("[10,20,30]", "$[0]"),
                SecondVal = FlinkSql.JsonValue("{\"a\":[10,20]}", "$.a[1]"),
                ArrJson = FlinkSql.JsonQuery("{\"a\":[10,20]}", "$.a"),
            }),
            "probe");

        yield return Ok("27_json_lax_strict_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Root1 = FlinkSql.JsonValue("[10,20,30]", "$[1]"),
                LaxRoot1 = FlinkSql.JsonValue("[10,20,30]", "lax $[1]"),
                LaxA1 = FlinkSql.JsonValue("{\"a\":[10,20]}", "lax $.a[1]"),
                StrictA1 = FlinkSql.JsonValue("{\"a\":[10,20]}", "strict $.a[1]"),
                QueryA1 = FlinkSql.JsonQuery("{\"a\":[10,20]}", "$.a[1]"),
            }),
            "probe");

        yield return Ok("28_json_object_array_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                RawJson = "{\"a\":[10,20]}",
                A_Array = FlinkSql.JsonQuery("{\"a\":[10,20]}", "$.a"),
                A0 = FlinkSql.JsonValue("{\"a\":[10,20]}", "$.a[0]"),
                A1 = FlinkSql.JsonValue("{\"a\":[10,20]}", "$.a[1]"),
                A1_AsInt = FlinkSql.Cast<int>(FlinkSql.JsonValue("{\"a\":[10,20]}", "$.a[1]"), FlinkSqlType.Int),
            }),
            "probe");

        yield return Ok("29_json_paths_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Root1 = FlinkSql.JsonValue("[10,20,30]", "$[1]"),
                LaxRoot1 = FlinkSql.JsonValue("[10,20,30]", "lax $[1]"),
                LaxA1 = FlinkSql.JsonValue("{\"a\":[10,20]}", "lax $.a[1]"),
                StrictA1 = FlinkSql.JsonValue("{\"a\":[10,20]}", "strict $.a[1]"),
                QueryA1 = FlinkSql.JsonQuery("{\"a\":[10,20]}", "$.a[1]"),
            }),
            "probe");

        yield return Fail("30_json_table_probe", b => b.From<ProbeRow>()
            .Select(_ => new { Table = FlinkSql.JsonTable("{\"a\":[10,20]}", "$.a[*]") }),
            "probe");

        yield return Fail("31_json_length_probe", b => b.From<ProbeRow>()
            .Select(_ => new { Len = FlinkSql.JsonLength("[1,2,3]") }),
            "probe");

        yield return Fail("32_json_array_length_probe", b => b.From<ProbeRow>()
            .Select(_ => new { Len = FlinkSql.JsonArrayLength("[1,2,3]") }),
            "probe");

        yield return Fail("33_json_keys_probe", b => b.From<ProbeRow>()
            .Select(_ => new { Keys = FlinkSql.JsonKeys("{\"a\":1,\"b\":2}") }),
            "probe");

        yield return Fail("34_json_object_keys_probe", b => b.From<ProbeRow>()
            .Select(_ => new { Keys = FlinkSql.JsonObjectKeys("{\"a\":1,\"b\":2}") }),
            "probe");

        yield return Ok("35_regex_operator_probe", b => b.From<ProbeRow>()
            .Select(_ => new { RegexpOp = FlinkSql.Regexp("abc-123", "^[a-z]+-[0-9]+$") }),
            "probe");

        yield return Ok("36_json_exists_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                ExistsA1 = FlinkSql.JsonExists("{\"a\":[10,20]}", "$.a[1]"),
                ExistsA9 = FlinkSql.JsonExists("{\"a\":[10,20]}", "$.a[9]"),
            }),
            "probe");

        yield return Ok("40_math_functions_flink_ok", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Round_2 = Math.Round(1.2345, 2),
                Floor_1_9 = Math.Floor(1.9),
                Ceil_1_1 = Math.Ceiling(1.1),
                Abs_3 = Math.Abs(-3),
                Pow_2_3 = Math.Pow(2, 3),
                Sqrt_9 = Math.Sqrt(9),
                Log_10 = Math.Log(10),
                Exp_1 = Math.Exp(1),
            }),
            "probe");

        yield return Ok("41_case_coalesce_nullif_flink_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                CaseWhen = 1 == 1 ? "ok" : "ng",
                Coalesced = (string?)null ?? "fallback",
                NullIf_Equal = FlinkSql.NullIf(1, 1),
                NullIf_NotEqual = FlinkSql.NullIf(1, 2),
            }),
            "probe");

        yield return Ok("42_try_cast_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                TryCastInt_Ok = FlinkSql.TryCast<int>("123", FlinkSqlType.Int),
                TryCastInt_Bad = FlinkSql.TryCast<int>("x", FlinkSqlType.Int),
                TryCastTimestamp = FlinkSql.TryCast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
            }),
            "probe");

        yield return Ok("43_timestamp_parse_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                CastTimestamp = FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                ToTimestamp_1Arg = FlinkSql.ToTimestamp("2025-01-02 03:04:05"),
                ToTimestamp_2Arg = FlinkSql.ToTimestamp("2025-01-02 03:04:05", "yyyy-MM-dd HH:mm:ss"),
                CurrentTimestamp = FlinkSql.CurrentTimestamp(),
            }),
            "probe");

        yield return Ok("44_from_unixtime_probe", b => b.From<ProbeRow>()
            .Select(_ => new { FromUnixTime_Seconds = FlinkSql.FromUnixtime(1700000000) }),
            "probe");

        yield return Ok("45_substring_index_base_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Sub_0_3 = "abcdef".Substring(0, 3),
                Sub_1_3 = "abcdef".Substring(1, 3),
                Sub_2_3 = "abcdef".Substring(2, 3),
            }),
            "probe");

        yield return Ok("46_decimal_overflow_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                DecOk = FlinkSql.Cast<decimal>(123.45m, FlinkSqlType.Decimal(10, 2)),
                DecTooBig_TryCast = FlinkSql.TryCast<decimal>("12345678901234567890.12", FlinkSqlType.Decimal(10, 2)),
            }),
            "probe");

        yield return Ok("47_array_map_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Arr = FlinkSql.Array(10, 20, 30),
                ArrLen = FlinkSql.ArrayLength(FlinkSql.Array(10, 20, 30)),
                ArrIdx1 = FlinkSql.ArrayElement(FlinkSql.Array(10, 20, 30), 1),
                M = FlinkSql.Map("a", 1, "b", 2),
                MapGetA = FlinkSql.MapGet(FlinkSql.Map("a", 1, "b", 2), "a"),
            }),
            "probe");

        yield return Fail("47b_array_index_0_fail", b => b.From<ProbeRow>()
            .Select(_ => new { ArrIdx0 = FlinkSql.ArrayElement(FlinkSql.Array(10, 20, 30), 0) }),
            "probe");

        yield return Ok("48_ifnull_probe", b => b.From<ProbeRow>()
            .Select(_ => new { IfNullValue = FlinkSql.IfNull(FlinkSql.Cast<string>(null!, FlinkSqlType.String), "fallback") }),
            "probe");

        yield return Fail("48b_nvl_fail", b => b.From<ProbeRow>()
            .Select(_ => new { NvlValue = FlinkSql.Nvl(FlinkSql.Cast<string>(null!, FlinkSqlType.String), "fallback") }),
            "probe");

        yield return Ok("49a_timestampdiff_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                DiffMin = FlinkSql.TimestampDiff(
                    FlinkTimeUnit.Minute,
                    FlinkSql.Cast<DateTime>("2025-01-02 03:00:00", FlinkSqlType.Timestamp),
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp)),
                DiffSec = FlinkSql.TimestampDiff(
                    FlinkTimeUnit.Second,
                    FlinkSql.Cast<DateTime>("2025-01-02 03:00:00", FlinkSqlType.Timestamp),
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp)),
            }),
            "probe");

        yield return Ok("49b_trunc_floor_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                FloorToDay = FlinkSql.FloorTo(
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                    FlinkTimeUnit.Day),
                FloorToHour = FlinkSql.FloorTo(
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                    FlinkTimeUnit.Hour),
            }),
            "probe");

        yield return Ok("50_date_add_sub_interval_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                DatePlus1 = FlinkSql.AddInterval(FlinkSql.DateLiteral("2025-01-02"), FlinkSql.Interval(1, FlinkTimeUnit.Day)),
                DateMinus1 = FlinkSql.SubInterval(FlinkSql.DateLiteral("2025-01-02"), FlinkSql.Interval(1, FlinkTimeUnit.Day)),
                TsPlus5Min = FlinkSql.AddInterval(
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                    FlinkSql.Interval(5, FlinkTimeUnit.Minute)),
            }),
            "probe");

        yield return Ok("51_string_null_semantics_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                ConcatNull = FlinkSql.Concat("a", FlinkSql.Cast<string>(null!, FlinkSqlType.String), "b"),
                ReplaceNull = FlinkSql.Cast<string>(null!, FlinkSqlType.String).Replace("a", "b"),
                LenNull = FlinkSql.Cast<string>(null!, FlinkSqlType.String).Length,
                TrimNull = FlinkSql.Cast<string>(null!, FlinkSqlType.String).Trim(),
            }),
            "probe");

        yield return Ok("52_timezone_at_time_zone_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                BaseTs = FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                ToUtcFromTokyo = FlinkSql.ToUtcTimestamp(
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                    "Asia/Tokyo"),
                FromUtcToTokyo = FlinkSql.FromUtcTimestamp(
                    FlinkSql.Cast<DateTime>("2025-01-02 03:04:05", FlinkSqlType.Timestamp),
                    "Asia/Tokyo"),
            }),
            "probe");

        yield return Ok("52b_table_local_time_zone_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Zone = "UTC",
                TsLtz = FlinkSql.ToTimestampLtz(1700000000000, 3),
            }),
            "probe");

        yield return Ok("53_epoch_millis_to_timestamp_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                TsLtzFromMillis = FlinkSql.ToTimestampLtz(1700000000000, 3),
                TsLtzFromSeconds = FlinkSql.ToTimestampLtz(1700000000, 0),
            }),
            "probe");

        yield return Ok("54_json_value_typed_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                A_Str = FlinkSql.JsonValue("{\"a\":10,\"b\":true,\"c\":\"x\"}", "$.a"),
                A_Int = FlinkSql.Cast<int>(FlinkSql.JsonValue("{\"a\":10,\"b\":true,\"c\":\"x\"}", "$.a"), FlinkSqlType.Int),
                B_Bool = FlinkSql.Cast<bool>(FlinkSql.JsonValue("{\"a\":10,\"b\":true,\"c\":\"x\"}", "$.b"), FlinkSqlType.Boolean),
                C_Str = FlinkSql.JsonValue("{\"a\":10,\"b\":true,\"c\":\"x\"}", "$.c"),
            }),
            "probe");

        yield return Ok("55_map_keys_values_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                M = FlinkSql.Map("a", 1, "b", 2),
                Keys = FlinkSql.MapKeys(FlinkSql.Map("a", 1, "b", 2)),
                Vals = FlinkSql.MapValues(FlinkSql.Map("a", 1, "b", 2)),
            }),
            "probe");

        yield return Ok("56_array_helpers_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                Arr = FlinkSql.Array(10, 20, 30),
                Contains20 = FlinkSql.ArrayContains(FlinkSql.Array(10, 20, 30), 20),
                Distincted = FlinkSql.ArrayDistinct(FlinkSql.Array(10, 20, 20, 30)),
                Joined = FlinkSql.ArrayJoin(FlinkSql.Array("a", "b", "c"), "-"),
            }),
            "probe");

        yield return Ok("57_like_escape_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                LikeLiteralUnderscore = FlinkSql.Like("a_b", "a^_b", "^"),
                LikeLiteralPercent = FlinkSql.Like("a%b", "a^%b", "^"),
                LikeWildcardUnderscore = FlinkSql.Like("a_b", "a_b"),
                LikeWildcardPercent = FlinkSql.Like("a%b", "a%b"),
            }),
            "probe");

        yield return Ok("60_window_hop", b => b.From<OrderWithTime>()
            .HopWindow(o => o.EventTime, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6))
            .GroupBy(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(o => new
            {
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                o.CustomerId,
                Cnt = FlinkAgg.Count(),
                TotalAmount = FlinkAgg.Sum(o.Amount),
            }),
            "orders");

        yield return Ok("61_window_session", b => b.From<OrderWithTime>()
            .SessionWindow(o => o.EventTime, TimeSpan.FromSeconds(10))
            .GroupBy(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(o => new
            {
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                o.CustomerId,
                Cnt = FlinkAgg.Count(),
            }),
            "orders");

        yield return Ok("61b_window_session_streaming", b => b.From<OrderWithTime>()
            .SessionWindow(o => o.EventTime, TimeSpan.FromSeconds(10))
            .GroupBy(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(o => new
            {
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                o.CustomerId,
                Cnt = FlinkAgg.Count(),
            }),
            "orders");

        yield return Fail("62_window_over_rows", b => b.From<OrderWithTime>()
            .Select(o => new
            {
                o.OrderId,
                o.CustomerId,
                o.Amount,
                SumLast3Rows = FlinkSql.OverRows(FlinkAgg.Sum(o.Amount), o.CustomerId, o.EventTime, 2),
            }),
            "orders");

        yield return Fail("63_window_over_range", b => b.From<OrderWithTime>()
            .Select(o => new
            {
                o.OrderId,
                o.Amount,
                SumLast2Sec = FlinkSql.OverRangeInterval(
                    FlinkAgg.Sum(o.Amount),
                    o.CustomerId,
                    o.EventTime,
                    FlinkSql.Interval(2, FlinkTimeUnit.Second)),
            }),
            "orders");

        yield return Fail("63b_window_over_range_numeric", b => b.From<OrderWithTime>()
            .Select(o => new
            {
                o.OrderId,
                o.TsMs,
                o.Amount,
                SumLast2s = FlinkSql.OverRangeNumeric(FlinkAgg.Sum(o.Amount), o.CustomerId, o.TsMs, 2000),
            }),
            "orders");

        yield return Ok("64_window_proctime_tumble", b => b.From<OrderWithProcTime>()
            .TumbleWindow(o => FlinkWindow.Proctime(), TimeSpan.FromSeconds(5))
            .GroupBy(o => new { o.CustomerId, WindowStart = FlinkWindow.Start(), WindowEnd = FlinkWindow.End() })
            .Select(o => new
            {
                WindowStart = FlinkWindow.Start(),
                WindowEnd = FlinkWindow.End(),
                o.CustomerId,
                Cnt = FlinkAgg.Count(),
            }),
            "orders");

        yield return Fail("90_ksql_instr_fail", b => b.From<ProbeRow>()
            .Select(_ => new { Instr = FlinkSql.KsqlInstr("abcdef", "cd") }),
            "probe");

        yield return Fail("91_ksql_len_fail", b => b.From<ProbeRow>()
            .Select(_ => new { Len = FlinkSql.KsqlLen("abc") }),
            "probe");

        yield return Fail("92_ksql_timestampToString_fail", b => b.From<ProbeRow>()
            .Select(_ => new { YearStr = FlinkSql.KsqlTimestampToString(1700000000000, "yyyy", "UTC") }),
            "probe");

        yield return Fail("93_ksql_dateadd_fail", b => b.From<ProbeRow>()
            .Select(_ => new { Added = FlinkSql.KsqlDateAdd("minute", 5, new DateTime(2025, 1, 2, 3, 4, 5)) }),
            "probe");

        yield return Fail("94_ksql_json_extract_string_fail", b => b.From<ProbeRow>()
            .Select(_ => new { Val = FlinkSql.KsqlJsonExtractString("{\"a\": {\"b\": \"x\"}}", "$.a.b") }),
            "probe");

        yield return Fail("95_ksql_ucase_lcase_probe", b => b.From<ProbeRow>()
            .Select(_ => new
            {
                U = FlinkSql.KsqlUcase("Abc"),
                L = FlinkSql.KsqlLcase("Abc"),
            }),
            "probe");

        yield return Fail("96_ksql_format_timestamp_fail", b => b.From<ProbeRow>()
            .Select(_ => new { WeekNum = FlinkSql.KsqlFormatTimestamp(new DateTime(2025, 1, 2, 3, 4, 5), "w", "UTC") }),
            "probe");

        yield return Fail("97_ksql_json_array_length_fail", b => b.From<ProbeRow>()
            .Select(_ => new { Len = FlinkSql.KsqlJsonArrayLength("[1,2,3]") }),
            "probe");

        yield return Fail("98_ksql_earliest_latest_fail", b => b.From<Order>()
            .GroupBy(o => o.CustomerId)
            .Select(o => new
            {
                o.CustomerId,
                FirstAmount = FlinkAgg.EarliestByOffset(o.Amount),
                LastAmount = FlinkAgg.LatestByOffset(o.Amount),
            }),
            "orders");
    }

    private static FlinkGoldenCase Ok(string name, Func<StreamingQueryBuilder, IStreamingQueryable> build, params string[] sourceTopics)
        => new(name, build, sourceTopics, ExpectFailure: false);

    private static FlinkGoldenCase Fail(string name, Func<StreamingQueryBuilder, IStreamingQueryable> build, params string[] sourceTopics)
        => new(name, build, sourceTopics, ExpectFailure: true);

    private static string NormalizeNewlines(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private sealed record FlinkGoldenCase(
        string Name,
        Func<StreamingQueryBuilder, IStreamingQueryable> Build,
        IReadOnlyList<string> SourceTopics,
        bool ExpectFailure);

    private sealed class Order
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public int Amount { get; set; }
    }

    private sealed class OrderWithTime
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public int Amount { get; set; }
        public DateTime EventTime { get; set; }
        public long TsMs { get; set; }
    }

    private sealed class OrderWithProcTime
    {
        public int CustomerId { get; set; }
    }

    private sealed class Customer
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Price
    {
        public string Symbol { get; set; } = string.Empty;
        public double Px { get; set; }
    }

    private sealed class ProbeRow
    {
        public string Name { get; set; } = string.Empty;
        public DateTime EventTime { get; set; } = new(2025, 1, 2, 3, 4, 5);
        public long TsMs { get; set; } = 1700000000000;
    }
}

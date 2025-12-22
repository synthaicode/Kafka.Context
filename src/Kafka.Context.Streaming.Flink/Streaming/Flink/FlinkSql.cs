namespace Kafka.Context.Streaming.Flink;

public static class FlinkSql
{
    public static string Concat(params object[] parts) => throw new NotSupportedException("Use in ToQuery only.");
    public static int Locate(string needle, string haystack) => throw new NotSupportedException("Use in ToQuery only.");
    public static int Position(string needle, string haystack) => throw new NotSupportedException("Use in ToQuery only.");

    public static string Lpad(string value, int length, string pad) => throw new NotSupportedException("Use in ToQuery only.");
    public static string Rpad(string value, int length, string pad) => throw new NotSupportedException("Use in ToQuery only.");
    public static string SplitIndex(string value, string delimiter, int index) => throw new NotSupportedException("Use in ToQuery only.");

    public static string RegexpExtract(string input, string pattern, int group) => throw new NotSupportedException("Use in ToQuery only.");
    public static string RegexpReplace(string input, string pattern, string replace) => throw new NotSupportedException("Use in ToQuery only.");
    public static bool SimilarTo(string input, string pattern) => throw new NotSupportedException("Use in ToQuery only.");
    public static bool Regexp(string input, string pattern) => throw new NotSupportedException("Use in ToQuery only.");

    public static string JsonValue(string json, string path) => throw new NotSupportedException("Use in ToQuery only.");
    public static string JsonQuery(string json, string path) => throw new NotSupportedException("Use in ToQuery only.");
    public static bool JsonExists(string json, string path) => throw new NotSupportedException("Use in ToQuery only.");
    public static int JsonLength(string json) => throw new NotSupportedException("Use in ToQuery only.");
    public static int JsonArrayLength(string json) => throw new NotSupportedException("Use in ToQuery only.");
    public static string JsonKeys(string json) => throw new NotSupportedException("Use in ToQuery only.");
    public static string JsonObjectKeys(string json) => throw new NotSupportedException("Use in ToQuery only.");
    public static object JsonTable(string json, string path) => throw new NotSupportedException("Use in ToQuery only.");

    public static T Cast<T>(object value, FlinkSqlType type) => throw new NotSupportedException("Use in ToQuery only.");
    public static T TryCast<T>(object value, FlinkSqlType type) => throw new NotSupportedException("Use in ToQuery only.");
    public static object ToTimestamp(string value) => throw new NotSupportedException("Use in ToQuery only.");
    public static object ToTimestamp(string value, string format) => throw new NotSupportedException("Use in ToQuery only.");
    public static object ToTimestampLtz(long epoch, int precision) => throw new NotSupportedException("Use in ToQuery only.");
    public static object FromUnixtime(long epochSeconds) => throw new NotSupportedException("Use in ToQuery only.");
    public static object CurrentTimestamp() => throw new NotSupportedException("Use in ToQuery only.");

    public static object DateFormat(object timestamp, string format) => throw new NotSupportedException("Use in ToQuery only.");
    public static object Extract(FlinkTimeUnit unit, object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object TimestampAdd(FlinkTimeUnit unit, int value, object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object TimestampDiff(FlinkTimeUnit unit, object start, object end) => throw new NotSupportedException("Use in ToQuery only.");
    public static object DayOfWeek(object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object DayOfYear(object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object WeekOfYearExtract(object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object WeekOfYearFormat(object timestamp) => throw new NotSupportedException("Use in ToQuery only.");

    public static object FloorTo(object timestamp, FlinkTimeUnit unit) => throw new NotSupportedException("Use in ToQuery only.");
    public static FlinkSqlInterval Interval(int value, FlinkTimeUnit unit) => throw new NotSupportedException("Use in ToQuery only.");
    public static object DateLiteral(string value) => throw new NotSupportedException("Use in ToQuery only.");
    public static object AddInterval(object value, FlinkSqlInterval interval) => throw new NotSupportedException("Use in ToQuery only.");
    public static object SubInterval(object value, FlinkSqlInterval interval) => throw new NotSupportedException("Use in ToQuery only.");

    public static object ToUtcTimestamp(object timestamp, string tz) => throw new NotSupportedException("Use in ToQuery only.");
    public static object FromUtcTimestamp(object timestamp, string tz) => throw new NotSupportedException("Use in ToQuery only.");
    public static object Uuid() => throw new NotSupportedException("Use in ToQuery only.");

    public static object IfNull(object value, object fallback) => throw new NotSupportedException("Use in ToQuery only.");
    public static object NullIf(object value, object other) => throw new NotSupportedException("Use in ToQuery only.");
    public static bool Like(string value, string pattern, string? escape = null) => throw new NotSupportedException("Use in ToQuery only.");

    public static object Array(params object[] items) => throw new NotSupportedException("Use in ToQuery only.");
    public static int ArrayLength(object array) => throw new NotSupportedException("Use in ToQuery only.");
    public static object ArrayElement(object array, int index) => throw new NotSupportedException("Use in ToQuery only.");
    public static bool ArrayContains(object array, object value) => throw new NotSupportedException("Use in ToQuery only.");
    public static object ArrayDistinct(object array) => throw new NotSupportedException("Use in ToQuery only.");
    public static string ArrayJoin(object array, string delimiter) => throw new NotSupportedException("Use in ToQuery only.");

    public static object Map(params object[] entries) => throw new NotSupportedException("Use in ToQuery only.");
    public static object MapKeys(object map) => throw new NotSupportedException("Use in ToQuery only.");
    public static object MapValues(object map) => throw new NotSupportedException("Use in ToQuery only.");
    public static object MapGet(object map, object key) => throw new NotSupportedException("Use in ToQuery only.");

    public static object OverRows(object value, object partitionBy, object orderBy, int preceding) => throw new NotSupportedException("Use in ToQuery only.");
    public static object OverRangeInterval(object value, object partitionBy, object orderBy, FlinkSqlInterval interval) => throw new NotSupportedException("Use in ToQuery only.");
    public static object OverRangeNumeric(object value, object partitionBy, object orderBy, int preceding) => throw new NotSupportedException("Use in ToQuery only.");

    public static object KsqlInstr(string input, string needle) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlLen(string input) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlTimestampToString(long epochMillis, string format, string timezone) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlDateAdd(string unit, int value, object timestamp) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlJsonExtractString(string json, string path) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlUcase(string input) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlLcase(string input) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlFormatTimestamp(object timestamp, string format, string timezone) => throw new NotSupportedException("Use in ToQuery only.");
    public static object KsqlJsonArrayLength(string json) => throw new NotSupportedException("Use in ToQuery only.");
    public static object Nvl(object value, object fallback) => throw new NotSupportedException("Use in ToQuery only.");
}

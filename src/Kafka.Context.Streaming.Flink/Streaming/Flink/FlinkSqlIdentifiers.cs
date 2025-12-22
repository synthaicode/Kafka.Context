using System;
using System.Collections.Generic;

namespace Kafka.Context.Streaming.Flink;

internal static class FlinkSqlIdentifiers
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "values",
        "coalesce",
    };

    public static string NormalizeIdentifier(string suggested)
    {
        if (string.IsNullOrWhiteSpace(suggested))
            return "t";

        var normalized = suggested.Trim().Replace('-', '_');
        if (ReservedWords.Contains(normalized))
            normalized = "t_" + normalized;

        return normalized;
    }

    public static string QuoteIdentifier(string identifier)
    {
        var escaped = identifier.Replace("`", "``", StringComparison.Ordinal);
        return $"`{escaped}`";
    }
}

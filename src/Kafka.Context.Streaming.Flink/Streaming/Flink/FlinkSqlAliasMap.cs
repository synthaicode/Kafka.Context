using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace Kafka.Context.Streaming.Flink;

internal static class FlinkSqlAliasMap
{
    public static Dictionary<ParameterExpression, string> Build(LambdaExpression lambda)
    {
        var map = new Dictionary<ParameterExpression, string>();
        for (var i = 0; i < lambda.Parameters.Count; i++)
            map[lambda.Parameters[i]] = "t" + i.ToString(CultureInfo.InvariantCulture);

        return map;
    }

    public static Dictionary<ParameterExpression, string> Build(IReadOnlyList<ParameterExpression> parameters)
    {
        var map = new Dictionary<ParameterExpression, string>();
        for (var i = 0; i < parameters.Count; i++)
            map[parameters[i]] = "t" + i.ToString(CultureInfo.InvariantCulture);

        return map;
    }
}

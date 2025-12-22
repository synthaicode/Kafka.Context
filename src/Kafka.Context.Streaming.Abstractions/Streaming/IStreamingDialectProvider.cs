using System.Threading;
using System.Threading.Tasks;

namespace Kafka.Context.Streaming;

public interface IStreamingDialectProvider
{
    string NormalizeObjectName(string suggestedObjectName);

    string GenerateDdl(
        StreamingQueryPlan plan,
        StreamingStatementKind kind,
        StreamingOutputMode outputMode,
        string objectName,
        string outputTopic);

    Task ExecuteAsync(string ddl, CancellationToken cancellationToken);
}

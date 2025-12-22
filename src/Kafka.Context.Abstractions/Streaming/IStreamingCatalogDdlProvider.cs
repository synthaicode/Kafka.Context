namespace Kafka.Context.Streaming;

public interface IStreamingCatalogDdlProvider
{
    IReadOnlyList<string> GenerateSourceDdls(IReadOnlyList<StreamingSourceDefinition> sources);
    IReadOnlyList<string> GenerateSinkDdls(IReadOnlyList<StreamingSinkDefinition> sinks);
}


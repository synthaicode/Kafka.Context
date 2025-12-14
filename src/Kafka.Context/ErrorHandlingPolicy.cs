namespace Kafka.Context;

internal sealed record ErrorHandlingPolicy
{
    public ErrorAction ErrorAction { get; init; } = ErrorAction.Skip;
    public bool RetryEnabled { get; init; } = false;
    public int RetryCount { get; init; } = 3;
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(1);
}

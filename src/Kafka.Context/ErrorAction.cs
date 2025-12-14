namespace Kafka.Context;

public enum ErrorAction
{
    Skip = 0,
    Retry = 1,
    DLQ = 2
}


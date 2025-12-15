namespace Kafka.Context.Messaging;

public sealed class WindowedKey
{
    public WindowedKey(Avro.Generic.GenericRecord key, long windowStartUnixMs, long? windowEndUnixMs, int? sequence)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        WindowStartUnixMs = windowStartUnixMs;
        WindowEndUnixMs = windowEndUnixMs;
        Sequence = sequence;
    }

    public Avro.Generic.GenericRecord Key { get; }
    public long WindowStartUnixMs { get; }
    public long? WindowEndUnixMs { get; }
    public int? Sequence { get; }
}

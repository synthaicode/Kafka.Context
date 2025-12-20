namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SchemaFingerprintAttribute : Attribute
{
    public SchemaFingerprintAttribute(string fingerprint)
        => Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));

    public string Fingerprint { get; }
}


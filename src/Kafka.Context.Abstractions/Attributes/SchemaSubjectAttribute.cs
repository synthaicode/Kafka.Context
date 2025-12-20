namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SchemaSubjectAttribute : Attribute
{
    public SchemaSubjectAttribute(string subject)
        => Subject = subject ?? throw new ArgumentNullException(nameof(subject));

    public string Subject { get; }
}


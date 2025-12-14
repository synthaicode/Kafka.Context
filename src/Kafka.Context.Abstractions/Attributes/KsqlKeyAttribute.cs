using System;

namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
[Obsolete("Use KafkaKeyAttribute instead.")]
public sealed class KsqlKeyAttribute : Attribute;

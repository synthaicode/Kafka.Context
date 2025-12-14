using System;

namespace Kafka.Context.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KsqlKeyAttribute : Attribute;


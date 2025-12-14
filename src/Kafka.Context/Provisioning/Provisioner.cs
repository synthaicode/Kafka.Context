using Kafka.Context.Infrastructure.SchemaRegistry;

namespace Kafka.Context.Provisioning;

internal static class Provisioner
{
    public static async Task ProvisionAsync(KafkaContext context, CancellationToken cancellationToken)
    {
        await KafkaProvisioner.ProvisionKafkaAsync(context, cancellationToken).ConfigureAwait(false);

        using var sr = new SchemaRegistryService(context.Options, context.LoggerFactory);
        await sr.ValidateConnectivityAsync(cancellationToken).ConfigureAwait(false);

        using var schemaProvisioner = new SchemaProvisioner(context.Options, context.LoggerFactory);

        foreach (var entityType in context.GetEntityTypes())
        {
            var topicName = context.GetTopicNameFor(entityType);
            await schemaProvisioner.ProvisionTopicSchemasAsync(topicName, entityType, cancellationToken).ConfigureAwait(false);
        }

        var dlqTopic = string.IsNullOrWhiteSpace(context.Options.DlqTopicName) ? "dead_letter_queue" : context.Options.DlqTopicName;
        await schemaProvisioner.ProvisionTopicSchemasAsync(dlqTopic, typeof(Kafka.Context.Messaging.DlqEnvelope), cancellationToken).ConfigureAwait(false);
    }
}

using Kafka.Context.Infrastructure.Admin;

namespace Kafka.Context.Provisioning;

internal static class KafkaProvisioner
{
    public static async Task ProvisionKafkaAsync(KafkaContext context, CancellationToken cancellationToken)
    {
        var admin = new KafkaAdminService(context.Options, context.LoggerFactory);
        admin.ValidateKafkaConnectivity();

        var expectations = context.GetEntityTypes()
            .Select(t => TopicExpectation.ForEntityType(t, context.Options))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        await admin.EnsureTopicsExistAsync(expectations, cancellationToken).ConfigureAwait(false);
        await admin.EnsureDlqTopicExistsAsync(cancellationToken).ConfigureAwait(false);
    }
}

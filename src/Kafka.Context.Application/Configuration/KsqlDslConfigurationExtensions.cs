using Microsoft.Extensions.Configuration;

namespace Kafka.Context.Configuration;

public static class KsqlDslConfigurationExtensions
{
    public const string DefaultSectionName = "KsqlDsl";

    public static KsqlDslOptions GetKsqlDslOptions(this IConfiguration configuration, string sectionName = DefaultSectionName)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

        var options = new KsqlDslOptions();
        configuration.GetSection(sectionName).Bind(options);
        return options;
    }
}

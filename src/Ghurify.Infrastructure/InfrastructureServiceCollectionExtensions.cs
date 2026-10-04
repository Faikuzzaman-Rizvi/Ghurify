using Ghurify.Application.Abstractions;
using Ghurify.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.Infrastructure;

/// <summary>
/// Single entry point for wiring up infrastructure. The API composition root calls this
/// and does not know which concrete adapters exist.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGhurifyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Required settings are validated when the host starts, not on first use,
        // so a bad deployment fails loudly and immediately.
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IDatabaseHealthProbe, DatabaseHealthProbe>();

        return services;
    }
}

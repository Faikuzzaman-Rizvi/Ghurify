using FluentValidation;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.Application;

/// <summary>
/// Registers the use cases and their validators. Adapters are wired separately, in
/// Ghurify.Infrastructure, so this layer stays unaware of how anything is implemented.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddGhurifyApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // A missing or too-short signing key or pepper stops the host from starting.
        services
            .AddOptions<IdentityOptions>()
            .Bind(configuration.GetSection(IdentityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<RequestOtpHandler>();
        services.AddScoped<VerifyOtpHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();

        services.AddScoped<IValidator<RequestOtpCommand>, RequestOtpCommandValidator>();
        services.AddScoped<IValidator<VerifyOtpCommand>, VerifyOtpCommandValidator>();

        // --- Trips ---
        services.AddScoped<TripViewer>();
        services.AddScoped<SearchTripsHandler>();
        services.AddScoped<GetTripHandler>();
        services.AddScoped<ListDestinationsHandler>();

        services.AddScoped<IValidator<SearchTripsQuery>, SearchTripsQueryValidator>();

        return services;
    }
}

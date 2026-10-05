using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;
using Ghurify.Infrastructure.Data;
using Ghurify.Infrastructure.Email;
using Ghurify.Infrastructure.Identity;
using Ghurify.Infrastructure.Repositories.Identity;
using Ghurify.Infrastructure.Repositories.Trips;
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
        IConfiguration configuration,
        bool isDevelopment)
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

        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            // A test mailbox (Ethereal, Mailtrap...) accepts every message and delivers none.
            // Fine on a developer machine; anywhere else every sign-up would quietly fail while
            // every send reported success. Refuse to start instead.
            .Validate(
                options => isDevelopment || !(options.IsConfigured && options.IsMailCatcher),
                "Email:Host points at a test mailbox that never delivers mail. Sign-in codes "
                + "would not reach anyone. Configure a real SMTP server outside Development.")
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDatabaseHealthProbe, DatabaseHealthProbe>();

        // --- Identity ---
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IOtpCodeService, OtpCodeService>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        AddOtpSender(services, configuration, isDevelopment);

        // --- Trips ---
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IDestinationRepository, DestinationRepository>();

        return services;
    }

    /// <summary>
    /// Chooses how sign-in codes are delivered.
    ///
    /// Real SMTP whenever credentials exist. Without them, Development writes the code to the
    /// console so a fresh clone works with no mail account, and every other environment gets a
    /// sender that throws, because silently accepting sign-ups nobody can complete is worse
    /// than refusing to start the flow.
    /// </summary>
    private static void AddOtpSender(
        IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (email.IsConfigured)
        {
            services.AddSingleton<IOtpSender, SmtpOtpSender>();
            return;
        }

        if (isDevelopment)
        {
            services.AddSingleton<IOtpSender, DevelopmentOtpSender>();
            return;
        }

        services.AddSingleton<IOtpSender, UnconfiguredOtpSender>();
    }
}

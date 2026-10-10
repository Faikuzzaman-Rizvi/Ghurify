using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;
using Ghurify.Infrastructure.Data;
using Ghurify.Infrastructure.Email;
using Ghurify.Infrastructure.Identity;
using Ghurify.Infrastructure.Repositories.Admin;
using Ghurify.Infrastructure.Repositories.Identity;
using Ghurify.Application.Bookings;
using Ghurify.Application.Notifications;
using Ghurify.Application.Payments;
using Ghurify.Infrastructure.Jobs;
using Ghurify.Infrastructure.Payments;
using Ghurify.Infrastructure.Repositories.Payments;
using Ghurify.Application.Chat;
using Ghurify.Infrastructure.Repositories.Bookings;
using Ghurify.Application.Social;
using Ghurify.Application.Admin;
using Ghurify.Application.Safety;
using Ghurify.Infrastructure.Jobs.Safety;
using Ghurify.Infrastructure.Jobs.Social;
using Ghurify.Infrastructure.Sms;
using Ghurify.Infrastructure.Media;
using Ghurify.Infrastructure.Repositories.Chat;
using Ghurify.Infrastructure.Repositories.Social;
using Ghurify.Infrastructure.Repositories.Notifications;
using Ghurify.Infrastructure.Repositories.Safety;
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

        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<IDbConnectionFactory>(provider => provider.GetRequiredService<SqlConnectionFactory>());
        // Fills the pool while the host starts, so the first visitor does not wait for it.
        services.AddHostedService<ConnectionPoolWarmUp>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDatabaseHealthProbe, DatabaseHealthProbe>();
        services.AddSingleton<IStorageHealthProbe, StorageHealthProbe>();

        // --- Identity ---
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddScoped<IVerificationDocumentRepository, VerificationDocumentRepository>();
        services.AddScoped<ISignInThrottle, SignInThrottle>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IOtpCodeService, OtpCodeService>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IStepUpTokens, StepUpTokens>();

        AddOtpSender(services, configuration, isDevelopment);

        // --- Profiles, roles and verification ---
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IUserAccessRepository, UserAccessRepository>();
        services.AddScoped<IVerificationRepository, VerificationRepository>();
        services.AddSingleton<INidHasher, NidHasher>();
        AddEkycProvider(services, configuration, isDevelopment);

        // --- Bookings and notifications ---
        services.AddScoped<JoinRequestRepository>();
        services.AddScoped<IJoinRequestRepository>(provider => provider.GetRequiredService<JoinRequestRepository>());
        services.AddScoped<IBookingHoldRepository>(provider => provider.GetRequiredService<JoinRequestRepository>());
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // --- Payments ---
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IPaymentHistoryRepository, PaymentHistoryRepository>();
        AddPaymentGateway(services, configuration, isDevelopment);
        services.AddScoped<PayoutRepository>();
        services.AddScoped<IPayoutRepository>(provider => provider.GetRequiredService<PayoutRepository>());
        services.AddScoped<IBookingCancellationRepository>(provider => provider.GetRequiredService<PayoutRepository>());
        services.AddScoped<ITripLifecycleRepository, TripLifecycleRepository>();
        services.AddScoped<ITravelMapRepository, TravelMapRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();

        // --- Social and media ---
        services.AddScoped<ISocialRepository, SocialRepository>();
        services.AddScoped<IProcessMediaJob, ProcessMediaJob>();
        AddMediaStorage(services, configuration);

        // --- Admin portal ---
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IStaffRoleRepository, StaffRoleRepository>();

        // --- The site's own settings ---
        services.AddScoped<Application.Site.ISiteSettingsRepository, Repositories.Site.SiteSettingsRepository>();
        // Backs SiteConfigService. One instance holds the whole site configuration, so this is
        // the first thing to move to Redis when the API runs on more than one machine.
        services.AddMemoryCache();

        // --- Safety ---
        services.AddScoped<ISafetyRepository, SafetyRepository>();
        services.AddScoped<ICloseDestinationJob, CloseDestinationJob>();
        if (isDevelopment)
        {
            services.AddSingleton<ISmsSender, DevelopmentSmsSender>();
        }
        else
        {
            // No SMS provider is chosen yet: texts are reported as not sent, and logged as errors.
            services.AddSingleton<ISmsSender, UnconfiguredSmsSender>();
        }

        // --- Background jobs ---
        services.AddGhurifyJobs(configuration);

        // --- Safety ---
        services.AddScoped<IAuditLog, AuditLogRepository>();

        // --- Trips ---
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IDestinationRepository, DestinationRepository>();

        return services;
    }

    /// <summary>
    /// Blob storage is optional: without a connection string, uploads answer "unavailable" and the
    /// rest of the API works (degrade, do not fail).
    /// </summary>
    private static void AddMediaStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));

        if (string.IsNullOrWhiteSpace(configuration[$"{StorageOptions.SectionName}:ConnectionString"]))
        {
            services.AddSingleton<IMediaStorage, UnconfiguredMediaStorage>();
            services.AddSingleton<IIdentityDocumentStorage, UnconfiguredIdentityDocumentStorage>();
            return;
        }

        services.AddSingleton<IMediaStorage, BlobMediaStorage>();
        services.AddSingleton<IIdentityDocumentStorage, BlobIdentityDocumentStorage>();
        services.AddHostedService<StoragePreparation>();
    }

    /// <summary>
    /// Chooses the payment gateway from Payments:Provider. The fake is also registered as itself so
    /// the sandbox endpoints and tests can drive it; the host refuses to start with it in production.
    /// </summary>
    private static void AddPaymentGateway(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var provider = configuration[$"{PaymentsOptions.SectionName}:Provider"] ?? PaymentsOptions.FakeProvider;

        if (string.Equals(provider, PaymentsOptions.SslCommerzProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPaymentGateway, SslCommerzGateway>();

            // Development maps the pretend page's endpoints whatever the provider (see Program).
            if (isDevelopment)
            {
                services.AddSingleton<FakePaymentGateway>();
            }

            return;
        }

        services.AddSingleton<FakePaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<FakePaymentGateway>());
    }

    /// <summary>
    /// Chooses the e-KYC provider. The fake (decides by test NID, contacts nobody) is the default
    /// in Development and must be asked for by name anywhere else, so production can never fall
    /// back to a provider that approves everybody. Elsewhere the default is manual review: every
    /// check waits for an admin to compare the uploaded ID photos. A provider name nothing here
    /// knows fails loudly at the first check rather than guessing.
    /// </summary>
    private static void AddEkycProvider(IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var provider = configuration["Ekyc:Provider"];

        if (string.Equals(provider, FakeEkycProvider.ProviderName, StringComparison.OrdinalIgnoreCase)
            || (isDevelopment && string.IsNullOrWhiteSpace(provider)))
        {
            services.AddSingleton<IEkycProvider, FakeEkycProvider>();
            return;
        }

        if (string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, StartVerificationHandler.ManualReview, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEkycProvider, ManualReviewEkycProvider>();
            return;
        }

        services.AddSingleton<IEkycProvider, UnconfiguredEkycProvider>();
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

        if (isDevelopment && !string.IsNullOrWhiteSpace(email.PickupDirectory))
        {
            services.AddSingleton<IOtpSender, PickupDirectoryOtpSender>();
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

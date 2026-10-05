using Ghurify.Application;
using Ghurify.Application.Identity;
using Ghurify.Infrastructure;
using Ghurify.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// .claude/rules/backend.md says never log OTPs. DevelopmentOtpSender deliberately breaks that
/// so the sign-in flow is usable with no mail account configured, and the only thing keeping
/// it out of staging and production is the environment check in AddGhurifyInfrastructure.
///
/// These tests are that guard. If someone changes the registration, this fails rather than a
/// live environment quietly starting to write sign-in codes into its logs.
/// </summary>
public sealed class OtpSenderRegistrationTests
{
    [Fact]
    public void OutsideDevelopment_WithNoSmtpConfigured_TheSenderThatLogsTheCodeIsNotRegistered()
    {
        using var provider = BuildProvider(isDevelopment: false, smtpConfigured: false);

        var sender = provider.GetRequiredService<IOtpSender>();

        Assert.IsNotType<DevelopmentOtpSender>(sender);
        Assert.IsType<UnconfiguredOtpSender>(sender);
    }

    [Fact]
    public async Task OutsideDevelopment_WithNoSmtpConfigured_SendingRefusesLoudly()
    {
        using var provider = BuildProvider(isDevelopment: false, smtpConfigured: false);
        var sender = provider.GetRequiredService<IOtpSender>();

        // Silently succeeding would let an environment accept sign-ups whose codes never
        // arrive, while every dashboard showed it as healthy.
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sender.SendOtpAsync(
                Email.Parse("rizvi@example.com"), "123456", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InDevelopment_WithNoSmtpConfigured_TheConsoleSenderIsUsed()
    {
        using var provider = BuildProvider(isDevelopment: true, smtpConfigured: false);

        var sender = provider.GetRequiredService<IOtpSender>();

        Assert.IsType<DevelopmentOtpSender>(sender);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WheneverSmtpIsConfigured_RealMailIsSent_InEveryEnvironment(bool isDevelopment)
    {
        // Credentials present means the developer wants real email, including locally.
        using var provider = BuildProvider(isDevelopment, smtpConfigured: true);

        var sender = provider.GetRequiredService<IOtpSender>();

        Assert.IsType<SmtpOtpSender>(sender);
    }

    [Fact]
    public void OutsideDevelopment_ATestMailbox_StopsTheApiFromStarting()
    {
        // Ethereal accepts every message and delivers none: every sign-up would fail silently.
        using var provider = BuildProvider(isDevelopment: false, smtpConfigured: true, host: "smtp.ethereal.email");

        var failure = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<EmailOptions>>().Value);

        Assert.Contains("test mailbox", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InDevelopment_ATestMailbox_IsAllowed()
    {
        using var provider = BuildProvider(isDevelopment: true, smtpConfigured: true, host: "smtp.ethereal.email");

        Assert.True(provider.GetRequiredService<IOptions<EmailOptions>>().Value.IsMailCatcher);
    }

    [Fact]
    public void OutsideDevelopment_ARealMailServer_IsAllowed()
    {
        using var provider = BuildProvider(isDevelopment: false, smtpConfigured: true, host: "smtp.gmail.com");

        Assert.False(provider.GetRequiredService<IOptions<EmailOptions>>().Value.IsMailCatcher);
    }

    private static ServiceProvider BuildProvider(bool isDevelopment, bool smtpConfigured, string? host = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "Server=localhost;Database=Ghurify;Integrated Security=true;",
            ["Identity:OtpPepper"] = new string('p', 32),
            ["Identity:JwtSigningKey"] = new string('k', 32),
        };

        if (smtpConfigured)
        {
            settings["Email:UserName"] = "ghurify@example.com";
            settings["Email:Password"] = "an-app-password";
        }

        if (host is not null)
        {
            settings["Email:Host"] = host;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGhurifyApplication(configuration);
        services.AddGhurifyInfrastructure(configuration, isDevelopment);

        return services.BuildServiceProvider();
    }
}

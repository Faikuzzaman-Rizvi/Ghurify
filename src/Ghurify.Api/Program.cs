using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ghurify.Api.Authorization;
using Ghurify.Api.Configuration;
using Ghurify.Api.Endpoints;
using Ghurify.Api.Middleware;
using Ghurify.Api.Realtime;
using Ghurify.Application;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Application.Payments;
using Ghurify.Infrastructure;
using Ghurify.Infrastructure.Configuration;
using Ghurify.Infrastructure.Email;
using Ghurify.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// A bootstrap logger captures failures that happen before configuration is read.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Local settings (connection string, SMTP credentials) from the gitignored .env file at the
    // repository root. Development only: deployed environments use App Service settings and
    // Key Vault. Real environment variables are re-added after it, so they still win.
    // DotEnv:Enabled=false switches it off: the integration tests set it, because the file
    // would otherwise override their test database and blank SMTP settings with the
    // developer's own, and the suite would write to a real database and send real email.
    if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue("DotEnv:Enabled", true))
    {
        builder.Configuration.AddInMemoryCollection(
            DotEnvFile.Load(builder.Environment.ContentRootPath, Directory.GetCurrentDirectory()));
        builder.Configuration.AddEnvironmentVariables();
    }

    builder.Host.UseSerilog((context, _, configuration) =>
        SerilogSetup.Configure(configuration, context.Configuration));

    // --- Options, validated at startup so a bad deployment fails immediately ---
    builder.Services
        .AddOptions<CorsOptions>()
        .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddGhurifyApplication(builder.Configuration);
    builder.Services.AddGhurifyInfrastructure(
        builder.Configuration,
        builder.Environment.IsDevelopment());

    // --- Errors: every expected failure leaves as ProblemDetails ---
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            // The trace id lets support match a user report to a log entry.
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });

    // Enums travel as their names ("WomenOnly", not 2): readable in the browser, stable in the
    // generated client, and a reordered enum can never silently change what a number means.
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddOpenApi();

    // --- Authentication: bearer tokens issued by the email-OTP sign-in ---
    var identityOptions = builder.Configuration
        .GetSection(IdentityOptions.SectionName)
        .Get<IdentityOptions>() ?? new IdentityOptions();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = identityOptions.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = identityOptions.JwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(identityOptions.JwtSigningKey)),
                ValidateLifetime = true,
                // The default five-minute grace would keep a 15-minute token alive for 20.
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            // Browsers cannot set headers on a WebSocket, so the SignalR client sends the access
            // token as ?access_token=. Accepted on the hub paths only, never on ordinary API calls,
            // where a token in a URL would end up in logs and browser history.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                },

                // A valid signature is not enough: the account must still be active. Without
                // this, a suspended or closed account would keep working until its access token
                // expired (up to 15 minutes). One indexed read per request, cached for the request
                // by AccessService, which the authorization policies reuse.
                OnTokenValidated = async context =>
                {
                    if (context.Principal?.FindUserId() is not { } userId)
                    {
                        context.Fail("The token names no user.");
                        return;
                    }

                    var access = context.HttpContext.RequestServices.GetRequiredService<AccessService>();
                    if (!(await access.GetAsync(userId, context.HttpContext.RequestAborted)).IsActive)
                    {
                        context.Fail("The account is not active.");
                    }
                },
            };
        });

    builder.Services.AddAuthorization(options => options.AddGhurifyPolicies());
    builder.Services.AddScoped<IAuthorizationHandler, AccessRequirementHandler>();

    // --- Real-time: SignalR hubs, and the adapters the use cases push through ---
    // Enums as names on the hubs too, matching the REST API and the generated client types.
    builder.Services.AddSignalR().AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
    builder.Services.AddSingleton<Ghurify.Application.Chat.IChatBroadcaster, SignalRChatBroadcaster>();
    builder.Services.AddSingleton<Ghurify.Application.Safety.ISafetyBroadcaster, SignalRSafetyBroadcaster>();

    // --- CORS: named policy, explicit origins, credentials allowed ---
    var corsOrigins = builder.Configuration
        .GetSection($"{CorsOptions.SectionName}:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
        options.AddPolicy(CorsOptions.PolicyName, policy => policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

    // --- Rate limiting: a global fallback now; auth, join-request and payment endpoints
    //     attach their own stricter policies as those features arrive. ---
    // Requests per minute per client IP across the API. Configurable so a load-test environment can
    // raise it (one load generator is one IP); production keeps the default.
    var globalPermitsPerMinute = Math.Max(60, builder.Configuration.GetValue("RateLimits:GlobalPerMinute", 300));

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Credential endpoints (sign-in, register, codes, password reset): far tighter than the
        // global budget, because they are the ones worth attacking. The per-address pause after
        // wrong passwords is separate and lives in the database. Many people can share one IP
        // behind a mobile carrier's NAT, so this is a coarse guard, not the main defence.
        options.AddPolicy(RateLimitPolicies.Auth, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        options.AddPolicy(RateLimitPolicies.Session, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        AddPerUserPolicy(options, RateLimitPolicies.Verification, permits: 5, window: TimeSpan.FromHours(1));
        AddPerUserPolicy(options, RateLimitPolicies.JoinRequests, permits: 20, window: TimeSpan.FromHours(1));
        AddPerUserPolicy(options, RateLimitPolicies.Payments, permits: 20, window: TimeSpan.FromMinutes(10));
        AddPerUserPolicy(options, RateLimitPolicies.Content, permits: 60, window: TimeSpan.FromMinutes(1));

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                // Partition by caller so one noisy client cannot starve the rest.
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    var app = builder.Build();

    // The local-development secrets in appsettings.Development.json are public (they are in the
    // repository). If one ever reaches another environment, refuse to start rather than sign
    // tokens and hash IDs with a key anyone can read.
    if (!app.Environment.IsDevelopment())
    {
        string[] secrets = ["Identity:OtpPepper", "Identity:JwtSigningKey", "Verification:NidPepper", "Verification:CallbackSecret"];
        var leaked = secrets.Where(key => app.Configuration[key]?.Contains("not-a-secret", StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (leaked.Count > 0)
        {
            throw new InvalidOperationException(
                $"Development secrets are configured outside Development: {string.Join(", ", leaked)}. Set real values from Key Vault.");
        }
    }

    // The fake gateway approves whatever the sandbox page says. It must never take real bookings.
    var paymentsProvider = app.Configuration["Payments:Provider"] ?? PaymentsOptions.FakeProvider;
    var sandboxPayments = string.Equals(paymentsProvider, PaymentsOptions.FakeProvider, StringComparison.OrdinalIgnoreCase);
    if (sandboxPayments && app.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "Payments:Provider is \"fake\" in Production. Configure \"sslcommerz\" with live credentials.");
    }

    WarnIfSignInCodesWillNotBeDelivered(app);

    app.UseSecurityHeaders();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    // Deliberately registered after the request logger, which makes it the INNER of the two.
    // A client disconnect is caught here first, so Serilog records the request as 499 rather
    // than logging an error for a 500 that never happened, and the global exception handler
    // never sees it at all.
    app.UseClientDisconnectHandling();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
    }
    else
    {
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.UseCors(CorsOptions.PolicyName);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthEndpoints();
    app.MapIdentityEndpoints();
    app.MapTripsEndpoints();
    app.MapProfileEndpoints();
    app.MapAdminEndpoints();
    app.MapBookingsEndpoints();
    app.MapNotificationsEndpoints();
    app.MapPaymentsEndpoints(sandboxEnabled: sandboxPayments);

    app.MapChatEndpoints();
    app.MapSocialEndpoints();
    app.MapSafetyEndpoints();
    app.MapAdminPortalEndpoints();

    app.MapHub<NotificationHub>(NotificationHub.Path);
    app.MapHub<ChatHub>(ChatHub.Path);
    app.MapHub<SafetyHub>(SafetyHub.Path);

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ghurify API terminated unexpectedly during startup.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Named so the integration tests can reach it through WebApplicationFactory.
/// </summary>
public partial class Program
{
    /// <summary>
    /// A fixed window per signed-in user (falling back to the IP for anonymous callers), for the
    /// endpoints where one account doing too much is the abuse to stop.
    /// </summary>
    private static void AddPerUserPolicy(RateLimiterOptions options, string name, int permits, TimeSpan window) =>
        options.AddPolicy(name, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.FindUserId()?.ToString(CultureInfo.InvariantCulture)
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = window,
                    QueueLimit = 0,
                }));

    /// <summary>
    /// Says once, at startup, where sign-in codes will actually go. A test mailbox reports every
    /// send as successful, so without this the only symptom is "the code never arrives".
    /// Outside Development a test mailbox stops startup instead (see EmailOptions validation).
    /// </summary>
    private static void WarnIfSignInCodesWillNotBeDelivered(WebApplication app)
    {
        var email = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;

        if (!email.IsConfigured)
        {
            Log.Warning(
                "Email is not configured: sign-in codes are written to this console, not emailed. "
                + "Set Email:UserName and Email:Password to send real mail.");
        }
        else if (email.IsMailCatcher)
        {
            Log.Warning(
                "Email:Host {Host} is a TEST mailbox. Sign-in codes are accepted there and NEVER "
                + "delivered to real inboxes. Configure a real SMTP server (see EmailOptions).",
                email.Host);
        }
        else
        {
            Log.Information("Account emails (confirmation and password reset codes) will be sent through {Host}:{Port}.", email.Host, email.Port);
        }
    }
}

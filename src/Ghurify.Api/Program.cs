using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ghurify.Api.Configuration;
using Ghurify.Api.Endpoints;
using Ghurify.Api.Middleware;
using Ghurify.Application;
using Ghurify.Application.Identity;
using Ghurify.Infrastructure;
using Ghurify.Infrastructure.Configuration;
using Ghurify.Infrastructure.Email;
using Ghurify.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    if (builder.Environment.IsDevelopment())
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
        });

    builder.Services.AddAuthorization();

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
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Sign-in endpoints: far tighter than the global budget, because they are the ones
        // worth attacking. The per-address limit is separate and lives in the database.
        options.AddPolicy(RateLimitPolicies.Auth, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                // Partition by caller so one noisy client cannot starve the rest.
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    var app = builder.Build();

    WarnIfSignInCodesWillNotBeDelivered(app);

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
        app.MapOpenApi();
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
            Log.Information("Sign-in codes will be emailed through {Host}:{Port}.", email.Host, email.Port);
        }
    }
}

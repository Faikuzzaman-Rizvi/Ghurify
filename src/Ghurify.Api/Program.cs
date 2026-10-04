using System.Globalization;
using System.Threading.RateLimiting;
using Ghurify.Api.Configuration;
using Ghurify.Api.Endpoints;
using Ghurify.Infrastructure;
using Ghurify.Infrastructure.Logging;
using Microsoft.AspNetCore.RateLimiting;
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

    builder.Host.UseSerilog((context, _, configuration) =>
        SerilogSetup.Configure(configuration, context.Configuration));

    // --- Options, validated at startup so a bad deployment fails immediately ---
    builder.Services
        .AddOptions<CorsOptions>()
        .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddGhurifyInfrastructure(builder.Configuration);

    // --- Errors: every expected failure leaves as ProblemDetails ---
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
        {
            // The trace id lets support match a user report to a log entry.
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });

    builder.Services.AddOpenApi();

    // No schemes are registered yet; phone-OTP JWT bearer arrives with the Identity feature.
    // Registering the services now keeps the pipeline below valid and the diff small later.
    builder.Services.AddAuthentication();
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

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

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
public partial class Program;

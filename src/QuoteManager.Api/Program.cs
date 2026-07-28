using System.Text;
using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using QuoteManager.Api.Auth;
using QuoteManager.Api.ErrorHandling;
using QuoteManager.Api.Observability;
using QuoteManager.Api.Quotes;
using QuoteManager.Infrastructure;
using QuoteManager.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

// Serilog is configured twice on purpose. This first, minimal logger exists only so that a crash
// during start-up — a bad connection string, a failed migration — is still recorded somewhere
// instead of vanishing. It is replaced by the fully configured logger once configuration is read.
Log.Logger = TelemetryConfiguration.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        TelemetryConfiguration.Configure(configuration, context.Configuration, context.HostingEnvironment));

    // AD-6: telemetry is OpenTelemetry-native. Azure Monitor is one exporter, attached only when
    // its connection string is present, so the absence of an Azure subscription is a supported
    // configuration rather than a start-up failure.
    var azureMonitorConnectionString = builder.Configuration["AzureMonitor:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(azureMonitorConnectionString))
    {
        builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
            options.ConnectionString = azureMonitorConnectionString);
    }

    builder.Services.AddInfrastructure(builder.Configuration);

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException($"Missing required configuration section '{JwtOptions.SectionName}'.");
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.AddSingleton<TokenService>();

    // AD-9: exactly one authentication scheme, JWT bearer, HS256. A fallback authorisation policy
    // requires an authenticated user for every endpoint, so protection is the default and
    // anonymity is opt-in via explicit AllowAnonymous() calls below.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

    // AD-7: actions cross the wire as readable names (e.g. "StartReview"), not the ordinal a
    // default JSON enum converter would emit, since the UI maps permittedActions to controls by
    // comparing these strings against QuoteAction.
    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddOpenApi();

    // AD-8: every error leaves the process as RFC 9457 problem details. DomainExceptionHandler
    // maps typed domain violations to their stable code and status first; AddProblemDetails is
    // what lets it (and the fallback handler for anything unmapped) actually write the response.
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Migrations and the demo seed run before the first request is served, so a reviewer who
    // clones the repository and runs one command lands on a populated application rather than an
    // empty one they have no credentials to fill (AD-16).
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }

    // Enriches each request log with the trace id and authenticated user, so a log line can always
    // be tied back to a distributed trace and an actor (AD-8, AD-10).
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = TelemetryConfiguration.EnrichRequestLog);

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Serves the built React bundle from wwwroot so a Release run is a single process on a single
    // origin — no CORS configuration, no second server to start during a demo. In development the
    // Vite dev server proxies to this host instead, so the browser still sees one origin.
    // Positioned ahead of authentication/authorisation, not just given AllowAnonymous() below: the
    // authorisation middleware applies the deny-by-default fallback policy to *every* request that
    // reaches it, including ones bound for a static file that never resolves to a routed endpoint,
    // so a physical asset request would 401 before UseStaticFiles ever got a chance to serve it.
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    // AD-9's complete anonymous set: login, health, the OpenAPI document, the Scalar reference UI,
    // and the SPA fallback route. Everything else is protected by the fallback policy above.
    app.MapOpenApi().AllowAnonymous();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.MapHealthChecks("/health").AllowAnonymous();

    app.MapAuthEndpoints();
    app.MapQuoteEndpoints();

    // MapFallbackToFile has the lowest route priority, so every API and OpenAPI route above wins
    // and only genuine client-side routes (e.g. /dashboard, with no matching physical file) fall
    // through to it.
    app.MapFallbackToFile("/index.html").AllowAnonymous();

    Log.Information("QuoteManager API starting in {Environment}", app.Environment.EnvironmentName);
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "QuoteManager API terminated unexpectedly during start-up");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed so WebApplicationFactory<Program> in the integration test assembly can host this app.
public partial class Program;

using System.Text;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Dashboard;
using QuoteManager.Api.ErrorHandling;
using QuoteManager.Api.Observability;
using QuoteManager.Api.Organizations;
using QuoteManager.Api.Quotes;
using QuoteManager.Api.Requests;
using QuoteManager.Api.Users;
using QuoteManager.Infrastructure;
using QuoteManager.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

const string ContentSecurityPolicy =
    "default-src 'self'; " +
    "script-src 'self'; " +
    "style-src 'self' 'unsafe-inline'; " +
    "img-src 'self' data:; " +
    "font-src 'self'; " +
    "connect-src 'self'; " +
    "object-src 'none'; " +
    "base-uri 'self'; " +
    "form-action 'self'; " +
    "frame-ancestors 'none'";

// This bootstrap logger exists only so a start-up crash - a bad connection string, a failed
// migration - is still recorded before the fully configured logger takes over.
Log.Logger = TelemetryConfiguration.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // If a Key Vault URI is configured, its secrets are layered on top of appsettings.json/user
    // secrets/env vars and take precedence for any matching key (e.g. a Jwt--SigningKey secret
    // maps to Jwt:SigningKey) - gated on its being present, the same "adapter only activates when
    // configured" pattern as the Azure Monitor connection string below. This has no real vault to
    // exercise in this environment, so only the "not configured" path - the actual default here -
    // is verified; DefaultAzureCredential resolves a managed identity in Azure and the logged-in
    // az/Visual Studio/VS Code credential for local development against a real vault.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }

    builder.Host.UseSerilog((context, services, configuration) =>
        TelemetryConfiguration.Configure(configuration, context.Configuration, context.HostingEnvironment));

    // Azure Monitor is an exporter swap on the same OpenTelemetry pipeline, gated on its
    // connection string being present (AD-6).
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

    // Exactly one auth scheme, JWT bearer; the fallback policy below requires an authenticated
    // user everywhere, so anonymity is explicit opt-in via AllowAnonymous() calls (AD-9).
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

    builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddOpenApi();

    // AddValidation wires the source-generated interceptor that runs DataAnnotations then
    // Validate() before a handler runs; a failure short-circuits to 400 without reaching
    // DomainExceptionHandler (AD-8).
    builder.Services.AddValidation();

    // DomainExceptionHandler maps domain violations to their stable code first; AddProblemDetails
    // covers everything else, per AD-8.
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Seeds the demo data before the first request, so a fresh clone starts populated (AD-16).
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }

    // Enriches each request log with the trace id and authenticated user, so a log line can always
    // be tied back to a distributed trace and an actor.
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = TelemetryConfiguration.EnrichRequestLog);

    // A baseline CSP on every response: this API is the actual security boundary, since it also
    // serves the built SPA (UseStaticFiles/MapFallbackToFile below). Scalar's dev-only API
    // reference page loads its UI bundle from a CDN, so it's excluded rather than widening the
    // policy for the rest of the app. style-src needs 'unsafe-inline' because Radix UI (used by
    // shadcn/ui's Select/Popover) positions its portaled content via inline style attributes.
    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/scalar") &&
            !context.Request.Path.StartsWithSegments("/openapi"))
        {
            context.Response.Headers.Append("Content-Security-Policy", ContentSecurityPolicy);
        }

        await next();
    });

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Must precede UseAuthentication/UseAuthorization (AD-9's own note on why).
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    // The complete anonymous set (AD-9): login, health, OpenAPI, Scalar, and the SPA fallback.
    app.MapOpenApi().AllowAnonymous();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.MapHealthChecks("/health").AllowAnonymous();

    app.MapAuthEndpoints();
    app.MapQuoteEndpoints();
    app.MapDashboardEndpoints();
    app.MapRequestEndpoints();
    app.MapRequestActivityEndpoints();
    app.MapOrganizationEndpoints();
    app.MapUserEndpoints();

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

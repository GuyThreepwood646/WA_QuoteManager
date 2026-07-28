using Azure.Monitor.OpenTelemetry.AspNetCore;
using QuoteManager.Api.Observability;
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

    builder.Services.AddOpenApi();

    // AD-8: every error leaves the process as RFC 9457 problem details. Registering the service
    // here is what allows the exception handler to produce them for unhandled failures too.
    builder.Services.AddProblemDetails();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Enriches each request log with the trace id and authenticated user, so a log line can always
    // be tied back to a distributed trace and an actor (AD-8, AD-10).
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = TelemetryConfiguration.EnrichRequestLog);

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.MapOpenApi();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference();
    }

    app.MapHealthChecks("/health").AllowAnonymous();

    // Serves the built React bundle from wwwroot so a Release run is a single process on a single
    // origin — no CORS configuration, no second server to start during a demo. In development the
    // Vite dev server proxies to this host instead, so the browser still sees one origin.
    // MapFallbackToFile has the lowest route priority, so every API and OpenAPI route above wins
    // and only genuine client-side routes fall through to the SPA.
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("/index.html");

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

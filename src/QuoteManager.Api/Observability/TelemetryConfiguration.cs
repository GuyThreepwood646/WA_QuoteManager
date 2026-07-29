using System.Diagnostics;
using System.Globalization;
using Serilog;
using Serilog.Events;

namespace QuoteManager.Api.Observability;

/// <summary>
/// Central Serilog composition.
///
/// This output is diagnostics only. It is deliberately NOT the audit trail: audit lives in the
/// AuditEntry table and is written inside the same transaction as the change it records, so it
/// can't disagree with committed state. Anything that queries "what happened" reads that table,
/// never these logs.
/// </summary>
public static class TelemetryConfiguration
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Logs are rendered with the invariant culture deliberately. Diagnostics that reformat numbers
    /// and dates according to whichever machine produced them are far harder to grep and correlate,
    /// and would differ between a developer laptop and a deployed host.
    /// </summary>
    private static readonly CultureInfo LogCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Minimal logger used only until configuration has been read, so a start-up crash is still
    /// recorded rather than lost.
    /// </summary>
    public static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: LogCulture)
            .CreateBootstrapLogger();

    public static void Configure(
        LoggerConfiguration logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        logger
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "QuoteManager.Api")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: LogCulture)
            // A rolling file sink means the demo has a durable, greppable record of client actions
            // and errors without depending on any cloud resource being reachable.
            .WriteTo.File(
                path: Path.Combine("logs", "quotemanager-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: ConsoleTemplate,
                formatProvider: LogCulture);
    }

    /// <summary>
    /// Attaches the correlation properties that make a request log line actionable: the trace id
    /// that ties it to a distributed trace, and the acting user.
    /// </summary>
    public static void EnrichRequestLog(IDiagnosticContext context, HttpContext httpContext)
    {
        context.Set("TraceId", Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);

        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            context.Set("UserId", user.FindFirst("sub")?.Value ?? user.Identity.Name);
        }
    }
}

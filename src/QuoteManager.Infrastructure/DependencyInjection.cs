using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Application.Abstractions;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence;
using QuoteManager.Infrastructure.Persistence.Auditing;

namespace QuoteManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Default database location, relative to the host's content root.
    /// </summary>
    /// <remarks>
    /// A default rather than a required setting so that a fresh clone runs with no configuration
    /// at all, which is the whole premise of the demo.
    /// </remarks>
    private const string DefaultConnectionString = "Data Source=quotemanager.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("QuoteManager") ?? DefaultConnectionString;

        // Single injected clock. Every time-derived value in the system reads from this, which is
        // what makes expiry and staleness signals reproducible under test.
        services.AddSingleton(TimeProvider.System);

        // AD-5: appends an AuditEntry per domain event inside the same SaveChanges call as the
        // change that raised it. Registered as a singleton per EF Core's own guidance for
        // stateless interceptors, and attached to every QuoteManagerDbContext instance.
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<QuoteManagerDbContext>((serviceProvider, options) =>
            options.UseSqlite(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<DatabaseInitializer>();

        // AD-10: ICurrentUser is a per-request adapter over the authenticated principal.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}

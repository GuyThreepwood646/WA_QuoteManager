using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Messaging;
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

        // AD-4/AD-5: appends an AuditEntry per domain event, and an OutboxMessage for those on the
        // integration allow-list, inside the same SaveChanges call as the change that raised them.
        // Registered as a singleton per EF Core's own guidance for stateless interceptors, and
        // attached to every QuoteManagerDbContext instance.
        services.AddSingleton<DomainEventPersistenceInterceptor>();
        services.AddDbContext<QuoteManagerDbContext>((serviceProvider, options) =>
            options.UseSqlite(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<DomainEventPersistenceInterceptor>()));

        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<DatabaseInitializer>();

        // AD-10: ICurrentUser is a per-request adapter over the authenticated principal.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddMessaging(configuration);

        return services;
    }

    /// <summary>
    /// AD-6: the one composition-root method that decides which <see cref="IIntegrationEventPublisher"/>
    /// adapter is active. An absent <see cref="ServiceBusOptions.ConnectionString"/> resolves the
    /// in-process channel adapter; a present one resolves the Azure Service Bus adapter. Nothing
    /// outside this method ever branches on that setting.
    /// </summary>
    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));
        var serviceBusOptions = configuration.GetSection(ServiceBusOptions.SectionName).Get<ServiceBusOptions>()
            ?? new ServiceBusOptions();

        if (!string.IsNullOrWhiteSpace(serviceBusOptions.ConnectionString))
        {
            services.AddSingleton(_ => new ServiceBusClient(serviceBusOptions.ConnectionString));
            services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<ServiceBusClient>()
                .CreateSender(serviceBusOptions.QueueName));
            services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
        }
        else
        {
            services.AddSingleton(Channel.CreateUnbounded<IntegrationEventEnvelope>());
            services.AddSingleton<IIntegrationEventPublisher, InProcessIntegrationEventPublisher>();
            services.AddHostedService<InProcessIntegrationEventLogger>();
        }

        services.AddHostedService<OutboxDispatcher>();
    }
}

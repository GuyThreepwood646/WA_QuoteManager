using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Application.Abstractions;
using QuoteManager.Infrastructure;
using QuoteManager.Infrastructure.Messaging;

namespace QuoteManager.Infrastructure.Tests.Messaging;

/// <summary>
/// AD-6's one directly testable claim about adapter selection: an absent Service Bus connection
/// string resolves the local adapter, a present one resolves the Azure adapter, and nothing else
/// in the codebase ever branches on that setting itself.
/// </summary>
public sealed class IntegrationEventPublisherSelectionTests
{
    /// <summary>
    /// Not a real namespace - the SDK validates connection-string shape at construction time but
    /// never dials out until a send is attempted, which this test deliberately never does.
    /// </summary>
    private const string FakeServiceBusConnectionString =
        "Endpoint=sb://fake-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ZmFrZWZha2VmYWtlZmFrZWZha2VmYWtlPQ==";

    [Fact]
    public void An_absent_connection_string_resolves_the_in_process_adapter()
    {
        var provider = BuildProvider(serviceBusConnectionString: null);

        var publisher = provider.GetRequiredService<IIntegrationEventPublisher>();

        publisher.ShouldBeOfType<InProcessIntegrationEventPublisher>();
    }

    [Fact]
    public void A_present_connection_string_resolves_the_service_bus_adapter()
    {
        var provider = BuildProvider(FakeServiceBusConnectionString);

        var publisher = provider.GetRequiredService<IIntegrationEventPublisher>();

        publisher.ShouldBeOfType<ServiceBusIntegrationEventPublisher>();
    }

    private static ServiceProvider BuildProvider(string? serviceBusConnectionString)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:QuoteManager"] = "Data Source=:memory:",
        };

        if (serviceBusConnectionString is not null)
        {
            settings["ServiceBus:ConnectionString"] = serviceBusConnectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}

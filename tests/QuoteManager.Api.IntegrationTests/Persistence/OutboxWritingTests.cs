using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

/// <summary>
/// Proves that only domain events on the integration allow-list produce an
/// <see cref="QuoteManager.Infrastructure.Persistence.Entities.OutboxMessage"/>, every such row is
/// eventually dispatched by <see cref="QuoteManager.Infrastructure.Messaging.OutboxDispatcher"/>
/// through the local adapter selected by default, and events off the allow-list (such as
/// <c>OrganizationCreated</c>, covered separately by <see cref="Auditing.AuditTests"/>) never
/// appear here at all.
/// </summary>
public sealed class OutboxWritingTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_seed_writes_outbox_rows_only_for_allow_listed_events()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var messages = await db.OutboxMessages.AsNoTracking().ToListAsync(ct);

        // Six requests, twelve vendor invitations, and the seed's one award: exactly what
        // DemoDataSeeder raises through Request.Create, InviteVendor and the Accept transition.
        messages.Count(m => m.Type == "RequestCreated.v1").ShouldBe(6);
        messages.Count(m => m.Type == "VendorInvited.v1").ShouldBe(12);
        messages.Count(m => m.Type == "QuoteAccepted.v1").ShouldBe(1);
        messages.Count(m => m.Type == "RequestAwarded.v1").ShouldBe(1);

        // The five OrganizationCreated events (asserted against AuditEntries by AuditTests) are
        // off the allow-list - they must never surface here under any contract name.
        messages.ShouldNotContain(m => m.Type.StartsWith("Organization", StringComparison.Ordinal));

        // Every allow-listed contract name observed is one of the five the allow-list can produce.
        string[] knownContracts =
        [
            "RequestCreated.v1", "VendorInvited.v1", "QuoteAccepted.v1", "RequestAwarded.v1", "RequestCancelled.v1",
        ];
        messages.ShouldAllBe(m => knownContracts.Contains(m.Type));
    }

    [Fact]
    public async Task The_outbox_dispatcher_eventually_marks_every_seeded_row_dispatched()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.CreateClient();

        // OutboxDispatcher polls every two seconds; poll here rather than sleeping once for exactly
        // that long, so the test is not a coin flip against the dispatcher's own timer.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        List<QuoteManager.Infrastructure.Persistence.Entities.OutboxMessage> messages = [];

        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
            messages = await db.OutboxMessages.AsNoTracking().ToListAsync(ct);

            if (messages.Count > 0 && messages.All(m => m.DispatchedAt is not null))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(m => m.DispatchedAt != null && m.Attempts == 0 && m.LastError == null);
    }
}

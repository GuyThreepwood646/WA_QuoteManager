using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Quotes;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Auditing;

/// <summary>
/// Proves that every domain event raised while building the seed graph lands as an
/// <see cref="QuoteManager.Infrastructure.Persistence.Entities.AuditEntry"/> in the same
/// transaction, with the actor resolved correctly whether it is the system sentinel, a user
/// created earlier in the very same save, or an existing user acting through the API.
/// </summary>
public sealed class AuditTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_seed_produces_an_audit_trail_with_correctly_resolved_actors()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var entries = await db.AuditEntries.AsNoTracking().ToListAsync(ct);

        entries.ShouldNotBeEmpty();

        // The five organizations are created with the DomainActor.System sentinel, before
        // any user account exists to attribute them to - the case ActorDisplayName resolution
        // must not silently get wrong just because there is no row to look up yet.
        entries.Count(e => e.Action == "OrganizationCreated" && e.ActorDisplayName == "System")
            .ShouldBe(5);

        // Every OrganizationCreated ActorId is the System sentinel, not a real user's id.
        entries.Where(e => e.Action == "OrganizationCreated")
            .ShouldAllBe(e => e.ActorId == Guid.Empty);

        // Vendors are invited by the admin user, created in the SAME SaveChangesAsync call as
        // these events - this is the case that would break a naive "query the database" lookup.
        entries.ShouldContain(e => e.Action == "VendorInvited" && e.ActorDisplayName == "Ada Admin");

        // The one award in the seed: an accepted quote's audit row correctly attributes the
        // reviewer, not the vendor who drafted the quote or the admin who invited them.
        var accepted = entries.Single(e => e.Action == "QuoteAccepted");
        accepted.ActorDisplayName.ShouldBe("Rae Reviewer");
        accepted.Summary.ShouldContain("UnderReview");
        accepted.Summary.ShouldContain("Accepted");

        // The superseded sibling is a distinct row with its own reason, not folded into the winner's.
        entries.ShouldContain(e => e.Action == "QuoteRejected" && e.Summary.Contains("SupersededByAcceptedQuote"));

        // Subject identity is real, not a placeholder: every quote-subject row's SubjectId is an
        // actual quote in the database.
        var quoteIds = await db.Quotes.AsNoTracking().Select(q => q.Id).ToListAsync(ct);
        entries.Where(e => e.SubjectType == "Quote").ShouldAllBe(e => quoteIds.Contains(e.SubjectId));
    }

    [Fact]
    public async Task A_transition_made_through_the_api_is_audited_with_the_authenticated_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestId, quoteId) = await FindQuoteAsync(
            "Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "reviewer@warehouseanywhere.test", password = QuoteManager.Infrastructure.Persistence.DemoDataSeeder.DemoPassword },
            ct);
        login.EnsureSuccessStatusCode();
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>(ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

        var getResponse = await client.GetAsync($"/api/requests/{requestId}/quotes/{quoteId}", ct);
        var before = await getResponse.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = "StartReview" }),
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"\"{before!.Version}\"");
        var actionResponse = await client.SendAsync(message, ct);
        actionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var auditRow = await db.AuditEntries.AsNoTracking()
            .Where(e => e.SubjectId == quoteId && e.Action == "QuoteUnderReview")
            .SingleAsync(ct);

        auditRow.ActorDisplayName.ShouldBe("Rae Reviewer");
        auditRow.SubjectType.ShouldBe("Quote");
    }

    private async Task<(Guid RequestId, Guid QuoteId)> FindQuoteAsync(string requestTitle, string vendorEmail)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var vendor = await db.Users.AsNoTracking().SingleAsync(u => u.Email == vendorEmail, cancellationToken);
        var request = await db.Requests.AsNoTracking().SingleAsync(r => r.Title == requestTitle, cancellationToken);
        var quote = request.Quotes.Single(q => q.VendorOrganizationId == vendor.OrganizationId);

        return (request.Id, quote.Id);
    }
}

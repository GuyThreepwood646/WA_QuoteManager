using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

public sealed class OrganizationProfilePersistenceTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Updating_an_organization_profile_with_locations_persists_through_ef()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var admin = await db.Users.AsNoTracking()
            .SingleAsync(u => u.Email == "admin@warehouseanywhere.test", ct);
        var actor = new DomainActor(admin.Id, admin.DisplayName, AppRole.Admin, OrganizationId: null);
        var now = time.GetUtcNow();

        var organization = Organization.Create("EF Profile Target", OrganizationKind.Vendor, actor, now);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var loaded = await db.Organizations
            .Include(o => o.Locations)
            .SingleAsync(o => o.Id == organization.Id, ct);
        loaded.UpdateProfile(
            "EF Profile Target",
            "55 Crate Lane, Greensboro, NC 27409",
            "Kim Olsen",
            "kim.update@crateworks.test",
            "+1 (336) 555-0163",
            false,
            [
                new OrganizationLocationInput("18 Packing Court, Columbia, SC 29201", "+1 (803) 555-0127"),
                new OrganizationLocationInput("9 Warehouse Row, Raleigh, NC 27603", "+1 (919) 555-0199"),
            ],
            actor,
            now);

        await db.EnsureNewLocationsAreAddedAsync(loaded, ct);
        await db.SaveChangesAsync(ct);

        var reloaded = await db.Organizations.AsNoTracking()
            .Include(o => o.Locations)
            .SingleAsync(o => o.Id == organization.Id, ct);
        reloaded.PrimaryContactEmail.ShouldBe("kim.update@crateworks.test");
        reloaded.Locations.Count.ShouldBe(2);
        reloaded.Locations.ShouldContain(l =>
            l.Address.Contains("Raleigh") && l.Phone == "+1 (919) 555-0199");
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Organizations;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Identity;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Populates a demo database with data a reviewer can act on immediately.
/// </summary>
/// <remarks>
/// The seed is load-bearing rather than decoration. A triage dashboard over an empty database
/// demonstrates nothing, and a reviewer who reaches a login screen holding no credentials cannot
/// start at all. So this produces one account per role, quotes occupying <em>every</em> lifecycle
/// state, one quote about to lapse, one request with competing quotes, and one request nobody has
/// answered — each of which exists to make a specific screen say something true.
///
/// Everything is built through the aggregates rather than inserted directly, so the seeded data is
/// necessarily legal under the transition table. A seeder writing rows straight to the database
/// can fabricate states the domain would refuse, and then the demo is exercising a fiction.
/// </remarks>
public sealed class DemoDataSeeder(
    QuoteManagerDbContext context,
    IPasswordHasher<AppUser> passwordHasher,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>
    /// The shared demo password. Published in the README; the whole point is that it is not secret.
    /// </summary>
    public const string DemoPassword = "Demo!2345";

    private const string Currency = "USD";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await context.Organizations.AnyAsync(cancellationToken))
        {
            PersistenceLog.SeedSkippedDataPresent(logger);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var system = DomainActor.System;

        var northwind = Organization.Create("Northwind Facilities", OrganizationKind.Client, system.Id, now);
        var contoso = Organization.Create("Contoso Health", OrganizationKind.Client, system.Id, now);
        var bolt = Organization.Create("Bolt Mechanical", OrganizationKind.Vendor, system.Id, now);
        var kestrel = Organization.Create("Kestrel HVAC", OrganizationKind.Vendor, system.Id, now);
        var ridgeline = Organization.Create("Ridgeline Electrical", OrganizationKind.Vendor, system.Id, now);

        context.Organizations.AddRange(northwind, contoso, bolt, kestrel, ridgeline);

        var admin = CreateUser("admin@quotemgr.test", "Ada Admin", AppRole.Admin, null);
        var requester = CreateUser("requester@quotemgr.test", "Riley Requester", AppRole.Requester, northwind.Id);
        var reviewer = CreateUser("reviewer@quotemgr.test", "Rae Reviewer", AppRole.Reviewer, contoso.Id);
        var boltUser = CreateUser("vendor@quotemgr.test", "Vic Vendor", AppRole.Vendor, bolt.Id);
        var kestrelUser = CreateUser("vendor2@quotemgr.test", "Kim Kestrel", AppRole.Vendor, kestrel.Id);
        var ridgelineUser = CreateUser("vendor3@quotemgr.test", "Rob Ridgeline", AppRole.Vendor, ridgeline.Id);

        context.Users.AddRange(admin, requester, reviewer, boltUser, kestrelUser, ridgelineUser);

        var adminActor = admin.ToActor();
        var reviewerActor = reviewer.ToActor();
        var boltActor = boltUser.ToActor();
        var kestrelActor = kestrelUser.ToActor();
        var ridgelineActor = ridgelineUser.ToActor();

        // A request mid-review: one quote being assessed, one waiting, one vendor silent. This is
        // the ordinary case the dashboard exists to summarise.
        var hvac = Request.Create(
            "Replace rooftop HVAC units",
            "Two 15-ton rooftop units at the Eastfield distribution centre.",
            northwind.Id, now.AddDays(21), requester.ToActor(), now.AddDays(-10));

        hvac.InviteVendor(bolt.Id, adminActor, now.AddDays(-10));
        hvac.InviteVendor(kestrel.Id, adminActor, now.AddDays(-10));
        hvac.InviteVendor(ridgeline.Id, adminActor, now.AddDays(-10));

        var hvacBolt = hvac.AddQuote(bolt.Id, Money(48_500m), now.AddDays(14), "Includes crane hire.", boltActor, now.AddDays(-8));
        hvac.ApplyQuoteAction(hvacBolt.Id, QuoteAction.Submit, boltActor, now.AddDays(-8));
        hvac.ApplyQuoteAction(hvacBolt.Id, QuoteAction.StartReview, reviewerActor, now.AddDays(-3));

        var hvacKestrel = hvac.AddQuote(kestrel.Id, Money(52_750m), now.AddDays(20), null, kestrelActor, now.AddDays(-6));
        hvac.ApplyQuoteAction(hvacKestrel.Id, QuoteAction.Submit, kestrelActor, now.AddDays(-6));

        // A completed award. Accepting rejected the competing quote automatically, which is the
        // single-accepted-quote invariant visible as history rather than described in a README.
        var electrical = Request.Create(
            "Annual electrical safety inspection",
            "Fixed-wiring inspection across three sites.",
            contoso.Id, now.AddDays(7), requester.ToActor(), now.AddDays(-14));

        electrical.InviteVendor(ridgeline.Id, adminActor, now.AddDays(-14));
        electrical.InviteVendor(kestrel.Id, adminActor, now.AddDays(-14));

        var electricalRidgeline = electrical.AddQuote(ridgeline.Id, Money(8_900m), now.AddDays(30), null, ridgelineActor, now.AddDays(-12));
        electrical.ApplyQuoteAction(electricalRidgeline.Id, QuoteAction.Submit, ridgelineActor, now.AddDays(-12));

        var electricalKestrel = electrical.AddQuote(kestrel.Id, Money(9_400m), now.AddDays(30), null, kestrelActor, now.AddDays(-11));
        electrical.ApplyQuoteAction(electricalKestrel.Id, QuoteAction.Submit, kestrelActor, now.AddDays(-11));

        electrical.ApplyQuoteAction(electricalRidgeline.Id, QuoteAction.StartReview, reviewerActor, now.AddDays(-9));
        electrical.ApplyQuoteAction(electricalRidgeline.Id, QuoteAction.Accept, reviewerActor, now.AddDays(-8));

        // Lapses in two days: the dashboard's "act now" case.
        var generator = Request.Create(
            "Emergency generator servicing",
            "Annual service and load bank test.",
            northwind.Id, now.AddDays(10), requester.ToActor(), now.AddDays(-3));

        generator.InviteVendor(bolt.Id, adminActor, now.AddDays(-3));
        var generatorBolt = generator.AddQuote(bolt.Id, Money(12_250m), now.AddDays(2), "Price held for 48 hours.", boltActor, now.AddDays(-2));
        generator.ApplyQuoteAction(generatorBolt.Id, QuoteAction.Submit, boltActor, now.AddDays(-2));

        // Still a draft, so the request remains editable and the vendor can still amend.
        var lobby = Request.Create(
            "Lobby refurbishment",
            "Reception desk, flooring and lighting.",
            contoso.Id, now.AddDays(60), requester.ToActor(), now.AddDays(-1));

        lobby.InviteVendor(kestrel.Id, adminActor, now.AddDays(-1));
        lobby.AddQuote(kestrel.Id, Money(76_000m), now.AddDays(45), "Draft pending site visit.", kestrelActor, now.AddDays(-1));

        // Nobody has responded at all. Without the invitation list this is indistinguishable from
        // a request nobody was asked about, which is the distinction that makes it actionable.
        var carPark = Request.Create(
            "Car park resurfacing",
            "Resurface and re-line the north car park.",
            northwind.Id, now.AddDays(45), requester.ToActor(), now.AddDays(-6));

        carPark.InviteVendor(bolt.Id, adminActor, now.AddDays(-6));
        carPark.InviteVendor(kestrel.Id, adminActor, now.AddDays(-6));
        carPark.InviteVendor(ridgeline.Id, adminActor, now.AddDays(-6));

        // Closed-out work, present so the terminal states are reachable in the UI.
        var windows = Request.Create(
            "Window cleaning contract",
            "Quarterly external clean, twelve-month term.",
            contoso.Id, now.AddDays(-5), requester.ToActor(), now.AddDays(-30));

        windows.InviteVendor(bolt.Id, adminActor, now.AddDays(-30));
        windows.InviteVendor(kestrel.Id, adminActor, now.AddDays(-30));

        var windowsBolt = windows.AddQuote(bolt.Id, Money(4_200m), now.AddDays(-2), null, boltActor, now.AddDays(-28));
        windows.ApplyQuoteAction(windowsBolt.Id, QuoteAction.Submit, boltActor, now.AddDays(-28));
        windows.ApplyQuoteAction(windowsBolt.Id, QuoteAction.Expire, adminActor, now.AddDays(-2));

        var windowsKestrel = windows.AddQuote(kestrel.Id, Money(4_800m), now.AddDays(10), null, kestrelActor, now.AddDays(-27));
        windows.ApplyQuoteAction(windowsKestrel.Id, QuoteAction.Submit, kestrelActor, now.AddDays(-27));
        windows.ApplyQuoteAction(windowsKestrel.Id, QuoteAction.Withdraw, kestrelActor, now.AddDays(-20));

        Request[] requests = [hvac, electrical, generator, lobby, carPark, windows];
        context.Requests.AddRange(requests);

        await context.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            var quoteCount = requests.Sum(request => request.Quotes.Count);
            PersistenceLog.SeedCompleted(logger, 5, 6, requests.Length, quoteCount);
        }
    }

    private static Money Money(decimal amount) => new(amount, Currency);

    private AppUser CreateUser(string email, string displayName, AppRole roles, Guid? organizationId)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
            Email = email,
            DisplayName = displayName,
            Roles = roles,
            OrganizationId = organizationId,
            PasswordHash = string.Empty,
        };

        // Hashed rather than stored, even for a throwaway demo account. A seeder is the most
        // commonly copied file in a codebase, and a plaintext password here becomes a plaintext
        // password in production by inheritance.
        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
        return user;
    }
}

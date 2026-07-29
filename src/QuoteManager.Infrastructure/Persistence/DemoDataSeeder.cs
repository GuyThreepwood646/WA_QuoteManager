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
/// Modelled on Warehouse Anywhere's actual business: client companies that need somewhere to store
/// goods across multiple markets submit a request, and Warehouse Anywhere's partner network of
/// storage facilities, packers and carriers respond with quotes. "Vendor" in this codebase is
/// therefore a storage, packing or transportation partner, not a general contractor.
///
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

        // Two client companies drawn straight from Warehouse Anywhere's stated verticals
        // (pharmaceutical sample management, retail/CPG), and three partner types representing the
        // three services WA actually brokers: a storage facility, a packing/crating vendor, and a
        // transportation/freight carrier.
        var meridian = Organization.Create("Meridian Pharma Sampling", OrganizationKind.Client, system.Id, now);
        var palmetto = Organization.Create("Palmetto Retail & CPG", OrganizationKind.Client, system.Id, now);
        var secureBase = Organization.Create("SecureBase Self Storage", OrganizationKind.Vendor, system.Id, now);
        var crateworks = Organization.Create("Crateworks Packing & Crating", OrganizationKind.Vendor, system.Id, now);
        var interstate = Organization.Create("Interstate Freight Partners", OrganizationKind.Vendor, system.Id, now);

        context.Organizations.AddRange(meridian, palmetto, secureBase, crateworks, interstate);

        var admin = CreateUser("admin@warehouseanywhere.test", "Ada Admin", AppRole.Admin, null);
        var requester = CreateUser("requester@warehouseanywhere.test", "Riley Requester", AppRole.Requester, meridian.Id);
        var reviewer = CreateUser("reviewer@warehouseanywhere.test", "Rae Reviewer", AppRole.Reviewer, palmetto.Id);
        var secureBaseUser = CreateUser("vendor@warehouseanywhere.test", "Vic Vendor", AppRole.Vendor, secureBase.Id);
        var crateworksUser = CreateUser("vendor2@warehouseanywhere.test", "Kim Crateworks", AppRole.Vendor, crateworks.Id);
        var interstateUser = CreateUser("vendor3@warehouseanywhere.test", "Rob Interstate", AppRole.Vendor, interstate.Id);

        context.Users.AddRange(admin, requester, reviewer, secureBaseUser, crateworksUser, interstateUser);

        var adminActor = admin.ToActor();
        var reviewerActor = reviewer.ToActor();
        var secureBaseActor = secureBaseUser.ToActor();
        var crateworksActor = crateworksUser.ToActor();
        var interstateActor = interstateUser.ToActor();

        // A request mid-review: one quote being assessed, one waiting, one partner silent. This is
        // the ordinary case the dashboard exists to summarise.
        var sampleStorage = Request.Create(
            "Regional sample storage — Southeast territory",
            "Secure, climate-controlled storage for field rep sample inventory across five sales territories, plus pack-out support for redistribution.",
            meridian.Id, now.AddDays(21), requester.ToActor(), now.AddDays(-10));

        sampleStorage.InviteVendor(secureBase.Id, adminActor, now.AddDays(-10));
        sampleStorage.InviteVendor(crateworks.Id, adminActor, now.AddDays(-10));
        sampleStorage.InviteVendor(interstate.Id, adminActor, now.AddDays(-10));

        var sampleStorageSecureBase = sampleStorage.AddQuote(
            secureBase.Id, Money(2_450m), now.AddDays(14),
            "Climate-controlled 4-unit block with 24/7 keycard access and rep check-in log.",
            secureBaseActor, now.AddDays(-8));
        sampleStorage.ApplyQuoteAction(sampleStorageSecureBase.Id, QuoteAction.Submit, secureBaseActor, now.AddDays(-8));
        sampleStorage.ApplyQuoteAction(sampleStorageSecureBase.Id, QuoteAction.StartReview, reviewerActor, now.AddDays(-3));

        var sampleStorageCrateworks = sampleStorage.AddQuote(
            crateworks.Id, Money(1_875m), now.AddDays(20),
            "Quarterly pack-out and redistribution to territory reps.",
            crateworksActor, now.AddDays(-6));
        sampleStorage.ApplyQuoteAction(sampleStorageCrateworks.Id, QuoteAction.Submit, crateworksActor, now.AddDays(-6));

        // A completed award. Accepting rejected the competing quote automatically, which is the
        // single-accepted-quote invariant visible as history rather than described in a README.
        var tradeShow = Request.Create(
            "Trade show fixture storage & drayage — West Coast expo season",
            "Off-season storage of retail fixtures and displays, plus drayage coordination to three expo venues.",
            palmetto.Id, now.AddDays(7), requester.ToActor(), now.AddDays(-14));

        tradeShow.InviteVendor(interstate.Id, adminActor, now.AddDays(-14));
        tradeShow.InviteVendor(crateworks.Id, adminActor, now.AddDays(-14));

        var tradeShowInterstate = tradeShow.AddQuote(
            interstate.Id, Money(8_900m), now.AddDays(30),
            "Includes drayage to three expo venues and 60-day short-term storage.",
            interstateActor, now.AddDays(-12));
        tradeShow.ApplyQuoteAction(tradeShowInterstate.Id, QuoteAction.Submit, interstateActor, now.AddDays(-12));

        var tradeShowCrateworks = tradeShow.AddQuote(
            crateworks.Id, Money(9_400m), now.AddDays(30),
            "Palletised storage and crate breakdown between venues.",
            crateworksActor, now.AddDays(-11));
        tradeShow.ApplyQuoteAction(tradeShowCrateworks.Id, QuoteAction.Submit, crateworksActor, now.AddDays(-11));

        tradeShow.ApplyQuoteAction(tradeShowInterstate.Id, QuoteAction.StartReview, reviewerActor, now.AddDays(-9));
        tradeShow.ApplyQuoteAction(tradeShowInterstate.Id, QuoteAction.Accept, reviewerActor, now.AddDays(-8));

        // Lapses in two days: the dashboard's "act now" case.
        var overflowStorage = Request.Create(
            "Overflow inventory storage — holiday peak season",
            "Short-term overflow storage for holiday peak season sample inventory; immediate turnaround needed.",
            meridian.Id, now.AddDays(10), requester.ToActor(), now.AddDays(-3));

        overflowStorage.InviteVendor(secureBase.Id, adminActor, now.AddDays(-3));
        var overflowStorageQuote = overflowStorage.AddQuote(
            secureBase.Id, Money(3_250m), now.AddDays(2),
            "Rate held for 48 hours — climate-controlled unit, month-to-month lease.",
            secureBaseActor, now.AddDays(-2));
        overflowStorage.ApplyQuoteAction(overflowStorageQuote.Id, QuoteAction.Submit, secureBaseActor, now.AddDays(-2));

        // Still a draft, so the request remains editable and the partner can still amend.
        var popUpStorage = Request.Create(
            "Pop-up retail storage & fixture staging",
            "Temporary storage and staging for pop-up retail fixtures ahead of a seasonal rollout.",
            palmetto.Id, now.AddDays(60), requester.ToActor(), now.AddDays(-1));

        popUpStorage.InviteVendor(crateworks.Id, adminActor, now.AddDays(-1));
        popUpStorage.AddQuote(
            crateworks.Id, Money(14_750m), now.AddDays(45),
            "Draft pending site visit — final crating and staging quote to follow.",
            crateworksActor, now.AddDays(-1));

        // Nobody has responded at all. Without the invitation list this is indistinguishable from
        // a request nobody was asked about, which is the distinction that makes it actionable.
        var coldChainPilot = Request.Create(
            "Cold-chain sample storage pilot — new territory launch",
            "Piloting a new sales territory; need a climate-controlled and refrigerated storage assessment before committing volume.",
            meridian.Id, now.AddDays(45), requester.ToActor(), now.AddDays(-6));

        coldChainPilot.InviteVendor(secureBase.Id, adminActor, now.AddDays(-6));
        coldChainPilot.InviteVendor(crateworks.Id, adminActor, now.AddDays(-6));
        coldChainPilot.InviteVendor(interstate.Id, adminActor, now.AddDays(-6));

        // Closed-out work, present so the terminal states are reachable in the UI.
        var seasonalLease = Request.Create(
            "Seasonal storage lease — spring reset",
            "Seasonal storage lease to cover a spring merchandising reset across regional stores.",
            palmetto.Id, now.AddDays(-5), requester.ToActor(), now.AddDays(-30));

        seasonalLease.InviteVendor(secureBase.Id, adminActor, now.AddDays(-30));
        seasonalLease.InviteVendor(crateworks.Id, adminActor, now.AddDays(-30));

        var seasonalLeaseSecureBase = seasonalLease.AddQuote(
            secureBase.Id, Money(4_650m), now.AddDays(-2), null, secureBaseActor, now.AddDays(-28));
        seasonalLease.ApplyQuoteAction(seasonalLeaseSecureBase.Id, QuoteAction.Submit, secureBaseActor, now.AddDays(-28));
        seasonalLease.ApplyQuoteAction(seasonalLeaseSecureBase.Id, QuoteAction.Expire, adminActor, now.AddDays(-2));

        var seasonalLeaseCrateworks = seasonalLease.AddQuote(
            crateworks.Id, Money(4_950m), now.AddDays(10), null, crateworksActor, now.AddDays(-27));
        seasonalLease.ApplyQuoteAction(seasonalLeaseCrateworks.Id, QuoteAction.Submit, crateworksActor, now.AddDays(-27));
        seasonalLease.ApplyQuoteAction(seasonalLeaseCrateworks.Id, QuoteAction.Withdraw, crateworksActor, now.AddDays(-20));

        Request[] requests = [sampleStorage, tradeShow, overflowStorage, popUpStorage, coldChainPilot, seasonalLease];
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

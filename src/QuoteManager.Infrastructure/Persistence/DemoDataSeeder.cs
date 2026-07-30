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
/// Populates a demo database with data a reviewer can act on immediately, modelled on Warehouse
/// Anywhere's actual business: client companies needing storage across multiple markets, and a
/// partner network of storage facilities, packers, and carriers responding with quotes — so
/// "Vendor" here means a storage, packing, or freight partner, not a general contractor. Built
/// through the aggregates rather than direct inserts, per AD-16.
/// </summary>
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

        // Two client companies drawn from Warehouse Anywhere's stated verticals (pharma sample
        // management, retail/CPG) and three vendor types for the three services WA brokers:
        // storage, packing/crating, and freight.
        var meridian = Organization.Create("Meridian Pharma Sampling", OrganizationKind.Client, system, now);
        var palmetto = Organization.Create("Palmetto Retail & CPG", OrganizationKind.Client, system, now);
        var secureBase = Organization.Create("SecureBase Self Storage", OrganizationKind.Vendor, system, now);
        var crateworks = Organization.Create("Crateworks Packing & Crating", OrganizationKind.Vendor, system, now);
        var interstate = Organization.Create("Interstate Freight Partners", OrganizationKind.Vendor, system, now);

        SeedOrganizationProfile(
            meridian,
            "1200 Peachtree Industrial Blvd, Suite 400, Atlanta, GA 30341",
            "Jordan Ellis",
            "jordan.ellis@meridianpharma.test",
            "+1 (404) 555-0182",
            false,
            [new("450 Research Parkway, Durham, NC 27709", "+1 (919) 555-0134")]);
        SeedOrganizationProfile(
            palmetto,
            "88 King Street, Charleston, SC 29401",
            "Morgan Blake",
            "morgan.blake@palmettoretail.test",
            "+1 (843) 555-0147",
            false,
            [
                new("2100 Commerce Drive, Charlotte, NC 28206", "+1 (704) 555-0171"),
                new("15 Harbor View Road, Savannah, GA 31401", "+1 (912) 555-0199"),
            ]);
        SeedOrganizationProfile(
            secureBase,
            "7420 Industrial Park Road, Charlotte, NC 28213",
            "Alex Rivera",
            "alex.rivera@securebase.test",
            "+1 (704) 555-0198",
            true,
            [
                new("910 Logistics Way, Raleigh, NC 27603", "+1 (919) 555-0148"),
                new("300 Storage Lane, Greenville, SC 29607", "+1 (864) 555-0166"),
            ]);
        SeedOrganizationProfile(
            crateworks,
            "55 Crate Lane, Greensboro, NC 27409",
            "Kim Olsen",
            "kim.olsen@crateworks.test",
            "+1 (336) 555-0163",
            false,
            [new("18 Packing Court, Columbia, SC 29201", "+1 (803) 555-0127")]);
        SeedOrganizationProfile(
            interstate,
            "400 Freight Terminal Drive, Spartanburg, SC 29303",
            "Rob Chen",
            "rob.chen@interstatefreight.test",
            "+1 (864) 555-0120",
            true,
            [new("2200 Interstate Blvd, Atlanta, GA 30336", "+1 (404) 555-0188")]);

        context.Organizations.AddRange(meridian, palmetto, secureBase, crateworks, interstate);

        var admin = CreateUser(
            "admin@warehouseanywhere.test", "Ada Admin", AppRole.Admin, null,
            "100 Commerce Center Drive, Suite 200, Charlotte, NC 28202", "+1 (704) 555-0111");
        var requester = CreateUser(
            "requester@warehouseanywhere.test", "Riley Requester", AppRole.Requester, meridian.Id,
            "1200 Peachtree Industrial Blvd, Suite 400, Atlanta, GA 30341", "+1 (404) 555-0133");
        var reviewer = CreateUser(
            "reviewer@warehouseanywhere.test", "Rae Reviewer", AppRole.Reviewer, palmetto.Id,
            "88 King Street, Charleston, SC 29401", "+1 (843) 555-0119");
        var secureBaseUser = CreateUser(
            "vendor@warehouseanywhere.test", "Vic Vendor", AppRole.Vendor, secureBase.Id,
            "7420 Industrial Park Road, Charlotte, NC 28213", "+1 (704) 555-0144");
        var crateworksUser = CreateUser(
            "vendor2@warehouseanywhere.test", "Kim Crateworks", AppRole.Vendor, crateworks.Id,
            "55 Crate Lane, Greensboro, NC 27409", "+1 (336) 555-0128");
        var interstateUser = CreateUser(
            "vendor3@warehouseanywhere.test", "Rob Interstate", AppRole.Vendor, interstate.Id,
            "400 Freight Terminal Drive, Spartanburg, SC 29303", "+1 (864) 555-0177");

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
        sampleStorage.ApplyQuoteAction(
            sampleStorageSecureBase.Id, QuoteAction.StartReview, reviewerActor, now.AddDays(-3),
            note: "Confirming climate-control specs with the facilities team before sign-off.");

        var sampleStorageCrateworks = sampleStorage.AddQuote(
            crateworks.Id, Money(1_875m), now.AddDays(20),
            "Quarterly pack-out and redistribution to territory reps.",
            crateworksActor, now.AddDays(-6));
        sampleStorage.ApplyQuoteAction(sampleStorageCrateworks.Id, QuoteAction.Submit, crateworksActor, now.AddDays(-6));

        // A completed award: accepting rejected the competing quote automatically (AD-3), visible
        // here as history rather than described in a README.
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
        tradeShow.ApplyQuoteAction(
            tradeShowInterstate.Id, QuoteAction.Accept, reviewerActor, now.AddDays(-8),
            note: "Best combination of price and included drayage coverage across the three venues.");

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

        // Nobody has responded — without the invitation list this would be indistinguishable from
        // a request nobody was asked about.
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
        seasonalLease.ApplyQuoteAction(
            seasonalLeaseCrateworks.Id, QuoteAction.Withdraw, crateworksActor, now.AddDays(-20),
            note: "Can no longer honor this rate for the requested window — crew capacity fell through.");

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

    private static void SeedOrganizationProfile(
        Organization organization,
        string primaryAddress,
        string primaryContactName,
        string primaryContactEmail,
        string primaryContactPhone,
        bool isPreferredVendor,
        OrganizationLocationInput[] locations)
    {
        organization.UpdateProfile(
            organization.Name,
            primaryAddress,
            primaryContactName,
            primaryContactEmail,
            primaryContactPhone,
            isPreferredVendor,
            locations,
            DomainActor.System,
            organization.CreatedAt);
    }

    private AppUser CreateUser(
        string email, string displayName, AppRole roles, Guid? organizationId,
        string? address = null, string? phone = null)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
            Email = email,
            DisplayName = displayName,
            Roles = roles,
            OrganizationId = organizationId,
            Address = address,
            Phone = phone,
            PasswordHash = string.Empty,
        };

        // Hashed even for a throwaway demo account: a seeder is the most commonly copied file in a
        // codebase, and a plaintext password here becomes one in production by inheritance.
        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);
        return user;
    }
}

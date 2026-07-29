using Microsoft.Extensions.Time.Testing;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Organizations;

namespace QuoteManager.Domain.Tests;

public sealed class OrganizationTests
{
    private static readonly DomainActor Admin =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Ada", AppRole.Admin, OrganizationId: null);

    private static readonly DomainActor Requester =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Riley", AppRole.Requester, OrganizationId: null);

    private static readonly DomainActor Vendor =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Vic", AppRole.Vendor,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

    private DateTimeOffset Now => _clock.GetUtcNow();

    [Fact]
    public void Admin_can_create_an_organization()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.Name.ShouldBe("Acme Storage");
        organization.Kind.ShouldBe(OrganizationKind.Vendor);
        organization.IsRetired.ShouldBeFalse();
    }

    [Fact]
    public void A_non_admin_cannot_create_an_organization()
    {
        Should.Throw<OrganizationActionNotPermittedException>(
            () => Organization.Create("Acme Storage", OrganizationKind.Vendor, Requester, Now));
    }

    [Fact]
    public void A_vendor_cannot_create_an_organization()
    {
        Should.Throw<OrganizationActionNotPermittedException>(
            () => Organization.Create("Acme Storage", OrganizationKind.Vendor, Vendor, Now));
    }

    [Fact]
    public void Admin_can_update_an_organization_profile()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.UpdateProfile(
            "Acme Storage & Logistics",
            "100 Warehouse Way, Austin, TX 78701",
            "Taylor Brooks",
            "taylor@acme.test",
            "+1 (512) 555-0100",
            true,
            [new("200 Dock Road, Dallas, TX 75201", "+1 (214) 555-0177")],
            Admin,
            Now);

        organization.Name.ShouldBe("Acme Storage & Logistics");
        organization.PrimaryAddress.ShouldBe("100 Warehouse Way, Austin, TX 78701");
        organization.PrimaryContactName.ShouldBe("Taylor Brooks");
        organization.IsPreferredVendor.ShouldBeTrue();
        organization.Locations.Count.ShouldBe(1);
        organization.Locations[0].Phone.ShouldBe("+1 (214) 555-0177");
    }

    [Fact]
    public void Preferred_vendor_is_ignored_for_client_organizations()
    {
        var organization = Organization.Create("Acme Client", OrganizationKind.Client, Admin, Now);

        organization.UpdateProfile(
            "Acme Client",
            null,
            null,
            null,
            null,
            true,
            [],
            Admin,
            Now);

        organization.IsPreferredVendor.ShouldBeFalse();
    }

    [Fact]
    public void A_non_admin_cannot_update_an_organization_profile()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        Should.Throw<OrganizationActionNotPermittedException>(() =>
            organization.UpdateProfile(
                "New Name",
                null,
                null,
                null,
                null,
                false,
                [],
                Requester,
                Now));
    }

    [Fact]
    public void Updating_profile_replaces_locations_whole_sale()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.UpdateProfile(
            "Acme Storage",
            null,
            null,
            null,
            null,
            false,
            [new("Site A", "+1 (111) 555-0001"), new("Site B", null)],
            Admin,
            Now);

        organization.Locations.Count.ShouldBe(2);
        organization.Locations[0].Address.ShouldBe("Site A");
        organization.Locations[1].Phone.ShouldBeNull();

        organization.UpdateProfile(
            "Acme Storage",
            null,
            null,
            null,
            null,
            false,
            [new("Site C only", "+1 (222) 555-0002")],
            Admin,
            Now);

        organization.Locations.Count.ShouldBe(1);
        organization.Locations[0].Address.ShouldBe("Site C only");
        organization.Locations[0].Phone.ShouldBe("+1 (222) 555-0002");
    }

    [Fact]
    public void Blank_location_addresses_are_ignored()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.UpdateProfile(
            "Acme Storage",
            null,
            null,
            null,
            null,
            false,
            [new("   ", "+1 (111) 555-0001"), new("Valid Site", null)],
            Admin,
            Now);

        organization.Locations.Count.ShouldBe(1);
        organization.Locations[0].Address.ShouldBe("Valid Site");
    }

    [Fact]
    public void Updating_profile_increments_version_when_the_name_is_unchanged()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);
        var versionAfterCreate = organization.Version;

        organization.UpdateProfile(
            "Acme Storage",
            "100 Warehouse Way, Austin, TX 78701",
            null,
            null,
            null,
            false,
            [],
            Admin,
            Now);

        organization.Version.ShouldBe(versionAfterCreate + 1);
    }

    [Fact]
    public void Admin_can_rename_an_organization()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.Rename("Acme Storage & Logistics", Admin, Now);

        organization.Name.ShouldBe("Acme Storage & Logistics");
    }

    [Fact]
    public void A_non_admin_cannot_rename_an_organization()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        Should.Throw<OrganizationActionNotPermittedException>(
            () => organization.Rename("New Name", Requester, Now));
    }

    [Fact]
    public void Admin_can_retire_an_organization()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        organization.Retire(Admin, Now);

        organization.IsRetired.ShouldBeTrue();
        organization.RetiredAt.ShouldBe(Now);
    }

    [Fact]
    public void A_non_admin_cannot_retire_an_organization()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);

        Should.Throw<OrganizationActionNotPermittedException>(
            () => organization.Retire(Requester, Now));
    }

    [Fact]
    public void Retiring_an_already_retired_organization_is_a_no_op()
    {
        var organization = Organization.Create("Acme Storage", OrganizationKind.Vendor, Admin, Now);
        organization.Retire(Admin, Now);
        var firstRetiredAt = organization.RetiredAt;

        _clock.Advance(TimeSpan.FromDays(1));
        organization.Retire(Admin, Now);

        organization.RetiredAt.ShouldBe(firstRetiredAt);
    }
}

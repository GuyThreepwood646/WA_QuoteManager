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

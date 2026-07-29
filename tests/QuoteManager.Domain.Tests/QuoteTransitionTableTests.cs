using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Domain.Tests;

/// <summary>
/// Tests the transition table directly, since it's the sole authority on the quote lifecycle.
/// </summary>
public sealed class QuoteTransitionTableTests
{
    private static readonly Guid OwnOrg = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrg = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DomainActor Admin =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Ada", AppRole.Admin, OrganizationId: null);

    private static readonly DomainActor Reviewer =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Rae", AppRole.Reviewer, OrganizationId: null);

    private static readonly DomainActor Vendor =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Vic", AppRole.Vendor, OwnOrg);

    private static readonly DomainActor OtherVendor =
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Kim", AppRole.Vendor, OtherOrg);

    private static readonly DomainActor Requester =
        new(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Riley", AppRole.Requester, OrganizationId: null);

    private static readonly DomainActor AllRoles =
        new(Guid.Parse("66666666-6666-6666-6666-666666666666"), "All", AppRole.All, OwnOrg);

    public static TheoryData<QuoteStatus> TerminalStates =>
    [
        QuoteStatus.Accepted,
        QuoteStatus.Rejected,
        QuoteStatus.Withdrawn,
        QuoteStatus.Expired,
    ];

    [Theory]
    [MemberData(nameof(TerminalStates))]
    public void Terminal_states_offer_no_actions_to_anyone(QuoteStatus status)
    {
        QuoteTransitions.IsTerminal(status).ShouldBeTrue();
        QuoteTransitions.PermittedFor(status, AllRoles, OwnOrg).ShouldBeEmpty();
    }

    [Fact]
    public void Accept_is_reachable_only_from_UnderReview()
    {
        var statesAllowingAccept = Enum.GetValues<QuoteStatus>()
            .Where(status => QuoteTransitions.PermittedFor(status, AllRoles, OwnOrg).Contains(QuoteAction.Accept))
            .ToArray();

        statesAllowingAccept.ShouldBe([QuoteStatus.UnderReview]);
    }

    [Fact]
    public void Accepting_a_Submitted_quote_is_refused_as_illegal_rather_than_unauthorised()
    {
        var resolution = QuoteTransitions.Resolve(QuoteStatus.Submitted, QuoteAction.Accept, Admin, OwnOrg);

        resolution.IsAllowed.ShouldBeFalse();
        resolution.IsDeniedByRole.ShouldBeFalse();
    }

    [Fact]
    public void A_vendor_cannot_accept_its_own_quote()
    {
        var resolution = QuoteTransitions.Resolve(QuoteStatus.UnderReview, QuoteAction.Accept, Vendor, OwnOrg);

        resolution.IsAllowed.ShouldBeFalse();
        resolution.IsDeniedByRole.ShouldBeTrue("the action is legal from this state, just not for this role");
    }

    [Fact]
    public void A_reviewer_cannot_submit_a_quote_on_a_vendors_behalf()
    {
        QuoteTransitions.Resolve(QuoteStatus.Draft, QuoteAction.Submit, Reviewer, OwnOrg)
            .IsDeniedByRole.ShouldBeTrue();
    }

    [Fact]
    public void A_requester_has_no_say_over_the_quote_lifecycle()
    {
        foreach (var status in Enum.GetValues<QuoteStatus>())
        {
            QuoteTransitions.PermittedFor(status, Requester, OwnOrg)
                .ShouldBeEmpty($"a requester should not act on quotes, but was offered actions in {status}");
        }
    }

    [Fact]
    public void Admin_can_perform_every_action_the_table_declares()
    {
        foreach (var transition in QuoteTransitions.All)
        {
            QuoteTransitions.Resolve(transition.From, transition.Action, Admin, OwnOrg)
                .IsAllowed.ShouldBeTrue($"admin should be permitted {transition.Action} from {transition.From}");
        }
    }

    [Fact]
    public void A_vendor_cannot_act_on_another_vendors_quote()
    {
        // Role matches, organization does not. Without the ownership check
        // this would succeed, and any Vendor could Withdraw a competitor's Submitted quote.
        var resolution = QuoteTransitions.Resolve(QuoteStatus.Submitted, QuoteAction.Withdraw, Vendor, OtherOrg);

        resolution.IsAllowed.ShouldBeFalse();
        resolution.IsDeniedByRole.ShouldBeTrue();
        QuoteTransitions.PermittedFor(QuoteStatus.Submitted, Vendor, OtherOrg).ShouldBeEmpty();
        QuoteTransitions.PermittedFor(QuoteStatus.Submitted, OtherVendor, OtherOrg)
            .ShouldContain(QuoteAction.Withdraw);
    }

    [Fact]
    public void A_vendor_without_an_organization_claim_cannot_take_vendor_gated_actions()
    {
        var orphan = new DomainActor(
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "Orphan",
            AppRole.Vendor,
            OrganizationId: null);

        QuoteTransitions.Resolve(QuoteStatus.Draft, QuoteAction.Submit, orphan, OwnOrg)
            .IsDeniedByRole.ShouldBeTrue();
    }

    [Fact]
    public void Every_declared_transition_leaves_the_state_it_came_from_except_Edit()
    {
        foreach (var transition in QuoteTransitions.All.Where(t => t.Action != QuoteAction.Edit))
        {
            transition.To.ShouldNotBe(transition.From, $"{transition.Action} should change state");
        }
    }

    [Fact]
    public void The_table_declares_no_duplicate_state_and_action_pair()
    {
        // A duplicate would make Resolve's first-match-wins silently shadow a row, so the table
        // could contain a rule that never fires and nobody would notice.
        var duplicates = QuoteTransitions.All
            .GroupBy(t => (t.From, t.Action))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void Only_Draft_is_editable()
    {
        var editable = Enum.GetValues<QuoteStatus>().Where(QuoteTransitions.IsEditable).ToArray();

        editable.ShouldBe([QuoteStatus.Draft]);
    }

    [Fact]
    public void Edit_appears_in_the_permitted_action_set_so_the_client_never_derives_it()
    {
        QuoteTransitions.PermittedFor(QuoteStatus.Draft, Vendor, OwnOrg).ShouldContain(QuoteAction.Edit);
        QuoteTransitions.PermittedFor(QuoteStatus.Submitted, Vendor, OwnOrg).ShouldNotContain(QuoteAction.Edit);
    }

    [Fact]
    public void Every_non_terminal_state_has_at_least_one_way_out()
    {
        foreach (var status in Enum.GetValues<QuoteStatus>().Where(s => !QuoteTransitions.IsTerminal(s)))
        {
            QuoteTransitions.PermittedFor(status, AllRoles, OwnOrg)
                .Where(action => action != QuoteAction.Edit)
                .ShouldNotBeEmpty($"{status} would be a dead end");
        }
    }
}

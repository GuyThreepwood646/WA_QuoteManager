using Microsoft.Extensions.Time.Testing;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Domain.Tests;

public sealed class RequestAggregateTests
{
    private static readonly Guid VendorOrgA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid VendorOrgB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DomainActor Reviewer =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Rae", AppRole.Reviewer, OrganizationId: null);

    private static readonly DomainActor Vendor =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Vic", AppRole.Vendor, VendorOrgA);

    private static readonly DomainActor VendorB =
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Kim", AppRole.Vendor, VendorOrgB);

    private static readonly DomainActor Admin =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Ada", AppRole.Admin, OrganizationId: null);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_new_request_is_Open_and_has_no_accepted_quote()
    {
        var request = NewRequest();

        request.Status.ShouldBe(RequestStatus.Open);
        request.AcceptedQuoteId.ShouldBeNull();
        request.Quotes.ShouldBeEmpty();
    }

    [Fact]
    public void Accepting_a_quote_rejects_the_live_siblings_and_awards_the_request()
    {
        var request = NewRequest();
        var winner = SubmitAndReview(request, 1000m, Vendor);
        var contender = Submit(request, 1200m, VendorB);
        var draft = request.AddQuote(VendorOrgA, new Money(900m, "USD"), null, null, Vendor, Now);

        request.ApplyQuoteAction(winner.Id, QuoteAction.Accept, Reviewer, Now);

        winner.Status.ShouldBe(QuoteStatus.Accepted);
        request.Status.ShouldBe(RequestStatus.Awarded);
        request.AcceptedQuoteId.ShouldBe(winner.Id);

        contender.Status.ShouldBe(QuoteStatus.Rejected);
        contender.StatusReason.ShouldBe("SupersededByAcceptedQuote");

        draft.Status.ShouldBe(QuoteStatus.Rejected);
        draft.StatusReason.ShouldBe("SupersededByAcceptedQuote");
    }

    [Fact]
    public void A_second_acceptance_is_refused_with_the_stable_already_accepted_code()
    {
        var request = NewRequest();
        var first = SubmitAndReview(request, 1000m, Vendor);
        var second = Submit(request, 1200m, VendorB);

        request.ApplyQuoteAction(first.Id, QuoteAction.Accept, Reviewer, Now);

        // The sibling was auto-rejected and the request was awarded, so a second Accept is blocked
        // at the request level before any quote transition runs.
        var exception = Should.Throw<DomainException>(() =>
            request.ApplyQuoteAction(second.Id, QuoteAction.Accept, Admin, Now));

        exception.Code.ShouldBe("request.not_editable");
        request.Quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);
    }

    [Fact]
    public void Accepting_never_leaves_more_than_one_accepted_quote_however_many_compete()
    {
        var request = NewRequest();
        var quotes = Enumerable.Range(0, 5)
            .Select(i => SubmitAndReview(request, 1000m + i, ActorForOrg(Guid.CreateVersion7())))
            .ToArray();

        request.ApplyQuoteAction(quotes[2].Id, QuoteAction.Accept, Reviewer, Now);

        request.Quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);
        request.Quotes.Where(q => q.Id != quotes[2].Id).ShouldAllBe(q => q.Status == QuoteStatus.Rejected);
    }

    [Fact]
    public void A_vendor_is_refused_when_accepting_and_the_state_is_left_untouched()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m, Vendor);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
                request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Vendor, Now))
            .BlockedByRole.ShouldBeTrue();

        quote.Status.ShouldBe(QuoteStatus.UnderReview);
        request.Status.ShouldBe(RequestStatus.Open);
    }

    [Fact]
    public void A_vendor_cannot_withdraw_another_vendors_quote()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m, Vendor);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
                request.ApplyQuoteAction(quote.Id, QuoteAction.Withdraw, VendorB, Now))
            .BlockedByRole.ShouldBeTrue();

        quote.Status.ShouldBe(QuoteStatus.Submitted);
    }

    [Fact]
    public void A_vendor_cannot_draft_a_quote_under_another_vendors_organization()
    {
        var request = NewRequest();

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
                request.AddQuote(VendorOrgB, new Money(1000m, "USD"), null, null, Vendor, Now))
            .BlockedByRole.ShouldBeTrue();

        request.Quotes.ShouldBeEmpty();
    }

    [Fact]
    public void A_requester_whose_organization_id_matches_the_target_still_cannot_draft_a_quote()
    {
        // Organization id is not exclusive to Vendor accounts - a Requester or Reviewer can carry
        // one too. CanActForVendorOrganization alone only compares ids, so without a role check
        // this actor's own organization id happening to equal vendorOrganizationId would otherwise
        // be enough to plant a quote despite holding no Vendor capability at all.
        var request = NewRequest();
        var requesterAtVendorOrgA = new DomainActor(
            Guid.Parse("55555555-5555-5555-5555-555555555555"), "Riley", AppRole.Requester, VendorOrgA);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
                request.AddQuote(VendorOrgA, new Money(1000m, "USD"), null, null, requesterAtVendorOrgA, Now))
            .BlockedByRole.ShouldBeTrue();

        request.Quotes.ShouldBeEmpty();
    }

    [Fact]
    public void A_quote_cannot_be_edited_once_submitted()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m, Vendor);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
            request.EditQuote(quote.Id, new Money(1m, "USD"), null, null, Vendor, Now));

        quote.Amount.Amount.ShouldBe(1000m);
    }

    [Fact]
    public void A_draft_quote_can_be_edited_by_its_vendor()
    {
        var request = NewRequest();
        var quote = request.AddQuote(VendorOrgA, new Money(1000m, "USD"), null, null, Vendor, Now);

        request.EditQuote(quote.Id, new Money(950.005m, "USD"), null, "Sharpened pencil", Vendor, Now);

        quote.Amount.Amount.ShouldBe(950.00m, "money is rounded to two places on the way in");
        quote.Notes.ShouldBe("Sharpened pencil");
    }

    [Fact]
    public void A_withdrawn_quote_can_be_revised_back_to_draft()
    {
        var request = NewRequest();
        var quote = request.AddQuote(VendorOrgA, new Money(1000m, "USD"), null, null, Vendor, Now);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Withdraw, Vendor, Now);
        quote.Status.ShouldBe(QuoteStatus.Withdrawn);

        request.EditQuote(quote.Id, new Money(875m, "USD"), null, "Revised offer", Vendor, Now);

        quote.Status.ShouldBe(QuoteStatus.Draft);
        quote.StatusReason.ShouldBeNull();
        quote.Amount.Amount.ShouldBe(875m);
        quote.Notes.ShouldBe("Revised offer");
        request.DomainEvents.ShouldContain(e => e.Action == "QuoteDraft");
    }

    [Fact]
    public void A_stale_expected_version_is_refused_before_any_state_changes()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m, Vendor);
        var staleVersion = quote.Version - 1;

        Should.Throw<QuoteConcurrencyException>(() =>
                request.ApplyQuoteAction(quote.Id, QuoteAction.StartReview, Reviewer, Now, staleVersion))
            .Code.ShouldBe("quote.concurrent_modification");

        quote.Status.ShouldBe(QuoteStatus.Submitted);
    }

    [Fact]
    public void A_matching_expected_version_is_accepted()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m, Vendor);

        request.ApplyQuoteAction(quote.Id, QuoteAction.StartReview, Reviewer, Now, quote.Version);

        quote.Status.ShouldBe(QuoteStatus.UnderReview);
    }

    [Fact]
    public void An_unknown_quote_id_is_a_domain_refusal_rather_than_a_null_reference()
    {
        var request = NewRequest();

        Should.Throw<QuoteNotFoundInRequestException>(() =>
                request.ApplyQuoteAction(Guid.NewGuid(), QuoteAction.Submit, Vendor, Now))
            .Code.ShouldBe("quote.not_found");
    }

    [Fact]
    public void A_request_stops_being_editable_once_a_vendor_has_submitted()
    {
        var request = NewRequest();
        request.IsEditable.ShouldBeTrue();

        var quote = request.AddQuote(VendorOrgA, new Money(1000m, "USD"), null, null, Vendor, Now);
        request.IsEditable.ShouldBeTrue("a draft nobody has committed to does not lock the scope");

        request.ApplyQuoteAction(quote.Id, QuoteAction.Submit, Vendor, Now);

        request.IsEditable.ShouldBeFalse();
        Should.Throw<RequestNotEditableException>(() =>
            request.Update("Rewritten scope", null, null, Admin, Now));
    }

    [Fact]
    public void Quotes_cannot_be_added_to_an_awarded_request()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m, Vendor);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Reviewer, Now);

        Should.Throw<RequestNotEditableException>(() =>
            request.AddQuote(VendorOrgB, new Money(1m, "USD"), null, null, VendorB, Now));
    }

    [Fact]
    public void An_awarded_request_cannot_be_cancelled()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m, Vendor);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Reviewer, Now);

        Should.Throw<RequestNotEditableException>(() => request.Cancel(Admin, Now));
    }

    [Fact]
    public void Every_state_change_raises_an_event_so_the_audit_trail_cannot_be_bypassed()
    {
        var request = NewRequest();
        var winner = SubmitAndReview(request, 1000m, Vendor);
        Submit(request, 1200m, VendorB);
        request.ApplyQuoteAction(winner.Id, QuoteAction.Accept, Reviewer, Now);

        var events = request.DomainEvents;

        events.ShouldContain(e => e.Action == nameof(RequestCreated));
        events.ShouldContain(e => e.Action == "QuoteAccepted");
        events.ShouldContain(e => e.Action == nameof(RequestAwarded));

        // The superseded sibling gets its own event, so the timeline explains why it was rejected
        // rather than leaving a state change with no recorded cause.
        events.Count(e => e.Action == "QuoteRejected").ShouldBe(1);
        events.ShouldAllBe(e => e.ActorId != Guid.Empty && e.OccurredAt == Now);
    }

    [Fact]
    public void Invited_vendors_who_have_not_quoted_are_the_signal_the_dashboard_needs()
    {
        var request = NewRequest();
        var responded = VendorOrgA;
        var silent = VendorOrgB;

        request.InviteVendor(responded, Admin, Now);
        request.InviteVendor(silent, Admin, Now);
        request.AddQuote(responded, new Money(1000m, "USD"), null, null, Vendor, Now);

        // A request with one quote looks identical whether one vendor was asked or five were;
        // this is what distinguishes those cases.
        request.Invitations.Count.ShouldBe(2);
        request.AwaitingResponseFrom.ShouldBe([silent]);
    }

    [Fact]
    public void Inviting_the_same_vendor_twice_is_a_harmless_no_op()
    {
        var request = NewRequest();
        var vendorId = Guid.CreateVersion7();

        request.InviteVendor(vendorId, Admin, Now);
        request.InviteVendor(vendorId, Admin, Now);

        request.Invitations.Count.ShouldBe(1);
        request.DomainEvents.Count(e => e.Action == nameof(VendorInvited)).ShouldBe(1);
    }

    [Fact]
    public void Vendors_cannot_be_invited_to_an_awarded_request()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m, Vendor);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Reviewer, Now);

        Should.Throw<RequestNotEditableException>(() =>
            request.InviteVendor(Guid.CreateVersion7(), Admin, Now));
    }

    [Fact]
    public void A_vendor_cannot_raise_a_request_on_behalf_of_a_client()
    {
        Should.Throw<RequestCreationNotPermittedException>(() =>
                Request.Create("Storage for Q4", null, Guid.NewGuid(), null, Vendor, Now))
            .Code.ShouldBe("request.creation_not_permitted");
    }

    [Fact]
    public void A_reviewer_cannot_raise_a_request_either()
    {
        // Reviewer moves quotes through review and decides the outcome; raising the request in
        // the first place belongs to Requester/Admin, so this is not a role that happens to be
        // adjacent - it is explicitly outside the gate.
        Should.Throw<RequestCreationNotPermittedException>(() =>
            Request.Create("Storage for Q4", null, Guid.NewGuid(), null, Reviewer, Now));
    }

    [Fact]
    public void A_vendor_cannot_update_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() =>
                request.Update("New title", null, null, Vendor, Now))
            .Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public void A_reviewer_cannot_update_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() =>
            request.Update("New title", null, null, Reviewer, Now));
    }

    [Fact]
    public void A_vendor_cannot_cancel_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() =>
                request.Cancel(Vendor, Now))
            .Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public void A_reviewer_cannot_cancel_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() => request.Cancel(Reviewer, Now));
    }

    [Fact]
    public void A_vendor_cannot_invite_a_vendor_to_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() =>
                request.InviteVendor(Guid.CreateVersion7(), Vendor, Now))
            .Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public void A_reviewer_cannot_invite_a_vendor_to_a_request()
    {
        var request = NewRequest();

        Should.Throw<RequestActionNotPermittedException>(() =>
            request.InviteVendor(Guid.CreateVersion7(), Reviewer, Now));
    }

    [Fact]
    public void Identifier_timestamps_come_from_the_injected_clock_and_not_the_wall_clock()
    {
        // UUIDv7 embeds a 48-bit millisecond timestamp in its leading bytes. Asserting on that
        // prefix is what catches a call to the parameterless Guid.CreateVersion7 overload, which
        // would compile, pass every other test, and silently reintroduce wall-clock time.
        var atNoon = Request.Create("A", null, Guid.Empty, null, Admin, Now).Id;
        var alsoAtNoon = Request.Create("B", null, Guid.Empty, null, Admin, Now).Id;

        TimestampBitsOf(atNoon).ShouldBe(TimestampBitsOf(alsoAtNoon));

        _clock.Advance(TimeSpan.FromHours(1));
        var anHourLater = Request.Create("C", null, Guid.Empty, null, Admin, Now).Id;

        TimestampBitsOf(anHourLater).ShouldBeGreaterThan(TimestampBitsOf(atNoon));
    }

    private static long TimestampBitsOf(Guid id)
    {
        var bytes = id.ToByteArray(bigEndian: true);
        long milliseconds = 0;

        for (var i = 0; i < 6; i++)
        {
            milliseconds = (milliseconds << 8) | bytes[i];
        }

        return milliseconds;
    }

    private static DomainActor ActorForOrg(Guid organizationId) =>
        new(Guid.CreateVersion7(), "Vendor", AppRole.Vendor, organizationId);

    private DateTimeOffset Now => _clock.GetUtcNow();

    private Request NewRequest() =>
        Request.Create("Replace the HVAC units", "Two rooftop units", Guid.NewGuid(), null, Admin, Now);

    private Quote Submit(Request request, decimal amount, DomainActor vendor)
    {
        var organizationId = vendor.OrganizationId
            ?? throw new InvalidOperationException("Submit helper requires a vendor with an organization.");

        var quote = request.AddQuote(organizationId, new Money(amount, "USD"), null, null, vendor, Now);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Submit, vendor, Now);
        return quote;
    }

    private Quote SubmitAndReview(Request request, decimal amount, DomainActor vendor)
    {
        var quote = Submit(request, amount, vendor);
        request.ApplyQuoteAction(quote.Id, QuoteAction.StartReview, Reviewer, Now);
        return quote;
    }
}

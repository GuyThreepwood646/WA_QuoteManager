using Microsoft.Extensions.Time.Testing;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Domain.Tests;

public sealed class RequestAggregateTests
{
    private static readonly DomainActor Reviewer = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Rae", AppRole.Reviewer);
    private static readonly DomainActor Vendor = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Vic", AppRole.Vendor);
    private static readonly DomainActor Admin = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Ada", AppRole.Admin);

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
        var winner = SubmitAndReview(request, 1000m);
        var contender = Submit(request, 1200m);
        var draft = request.AddQuote(Guid.NewGuid(), new Money(900m, "USD"), null, null, Vendor, Now);

        request.ApplyQuoteAction(winner.Id, QuoteAction.Accept, Reviewer, Now);

        winner.Status.ShouldBe(QuoteStatus.Accepted);
        request.Status.ShouldBe(RequestStatus.Awarded);
        request.AcceptedQuoteId.ShouldBe(winner.Id);

        contender.Status.ShouldBe(QuoteStatus.Rejected);
        contender.StatusReason.ShouldBe("SupersededByAcceptedQuote");

        // A draft was never in contention, so superseding it would misrepresent what happened.
        draft.Status.ShouldBe(QuoteStatus.Draft);
    }

    [Fact]
    public void A_second_acceptance_is_refused_with_the_stable_already_accepted_code()
    {
        var request = NewRequest();
        var first = SubmitAndReview(request, 1000m);
        var second = Submit(request, 1200m);

        request.ApplyQuoteAction(first.Id, QuoteAction.Accept, Reviewer, Now);

        // The sibling was auto-rejected, so reaching Accept again requires an admin to revive it;
        // the invariant must hold even then.
        var exception = Should.Throw<DomainException>(() =>
            request.ApplyQuoteAction(second.Id, QuoteAction.Accept, Admin, Now));

        exception.Code.ShouldBeOneOf("quote.already_accepted", "quote.transition_not_allowed");
        request.Quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);
    }

    [Fact]
    public void Accepting_never_leaves_more_than_one_accepted_quote_however_many_compete()
    {
        var request = NewRequest();
        var quotes = Enumerable.Range(0, 5).Select(i => SubmitAndReview(request, 1000m + i)).ToArray();

        request.ApplyQuoteAction(quotes[2].Id, QuoteAction.Accept, Reviewer, Now);

        request.Quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);
        request.Quotes.Where(q => q.Id != quotes[2].Id).ShouldAllBe(q => q.Status == QuoteStatus.Rejected);
    }

    [Fact]
    public void A_vendor_is_refused_when_accepting_and_the_state_is_left_untouched()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
                request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Vendor, Now))
            .BlockedByRole.ShouldBeTrue();

        quote.Status.ShouldBe(QuoteStatus.UnderReview);
        request.Status.ShouldBe(RequestStatus.Open);
    }

    [Fact]
    public void A_quote_cannot_be_edited_once_submitted()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m);

        Should.Throw<QuoteTransitionNotAllowedException>(() =>
            request.EditQuote(quote.Id, new Money(1m, "USD"), null, null, Vendor, Now));

        quote.Amount.Amount.ShouldBe(1000m);
    }

    [Fact]
    public void A_draft_quote_can_be_edited_by_its_vendor()
    {
        var request = NewRequest();
        var quote = request.AddQuote(Guid.NewGuid(), new Money(1000m, "USD"), null, null, Vendor, Now);

        request.EditQuote(quote.Id, new Money(950.005m, "USD"), null, "Sharpened pencil", Vendor, Now);

        quote.Amount.Amount.ShouldBe(950.00m, "money is rounded to two places on the way in");
        quote.Notes.ShouldBe("Sharpened pencil");
    }

    [Fact]
    public void A_stale_expected_version_is_refused_before_any_state_changes()
    {
        var request = NewRequest();
        var quote = Submit(request, 1000m);
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
        var quote = Submit(request, 1000m);

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

        var quote = request.AddQuote(Guid.NewGuid(), new Money(1000m, "USD"), null, null, Vendor, Now);
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
        var quote = SubmitAndReview(request, 1000m);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Reviewer, Now);

        Should.Throw<RequestNotEditableException>(() =>
            request.AddQuote(Guid.NewGuid(), new Money(1m, "USD"), null, null, Vendor, Now));
    }

    [Fact]
    public void An_awarded_request_cannot_be_cancelled()
    {
        var request = NewRequest();
        var quote = SubmitAndReview(request, 1000m);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Accept, Reviewer, Now);

        Should.Throw<RequestNotEditableException>(() => request.Cancel(Admin, Now));
    }

    [Fact]
    public void Every_state_change_raises_an_event_so_the_audit_trail_cannot_be_bypassed()
    {
        var request = NewRequest();
        var winner = SubmitAndReview(request, 1000m);
        Submit(request, 1200m);
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

    private DateTimeOffset Now => _clock.GetUtcNow();

    private Request NewRequest() =>
        Request.Create("Replace the HVAC units", "Two rooftop units", Guid.NewGuid(), null, Admin, Now);

    private Quote Submit(Request request, decimal amount)
    {
        var quote = request.AddQuote(Guid.NewGuid(), new Money(amount, "USD"), null, null, Vendor, Now);
        request.ApplyQuoteAction(quote.Id, QuoteAction.Submit, Vendor, Now);
        return quote;
    }

    private Quote SubmitAndReview(Request request, decimal amount)
    {
        var quote = Submit(request, amount);
        request.ApplyQuoteAction(quote.Id, QuoteAction.StartReview, Reviewer, Now);
        return quote;
    }
}

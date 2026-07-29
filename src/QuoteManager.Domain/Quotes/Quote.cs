using QuoteManager.Domain.Common;

namespace QuoteManager.Domain.Quotes;

/// <summary>
/// A vendor's offer against a request.
/// </summary>
/// <remarks>
/// Deliberately not an aggregate root. Accepting a quote has to see every sibling quote to
/// enforce "at most one accepted", so <c>Request</c> owns the quotes and is the consistency
/// boundary. Every mutating method here is internal — the only way to change a quote is through
/// its parent request.
/// </remarks>
public sealed class Quote : Entity
{
    private Quote(
        Guid id,
        Guid requestId,
        Guid vendorOrganizationId,
        Money amount,
        DateTimeOffset? expiresAt,
        string? notes,
        DateTimeOffset createdAt)
        : base(id)
    {
        RequestId = requestId;
        VendorOrganizationId = vendorOrganizationId;
        Amount = amount;
        ExpiresAt = expiresAt;
        Notes = notes;
        Status = QuoteStatus.Draft;
        CreatedAt = createdAt;
        StatusChangedAt = createdAt;
    }

    // EF Core materialisation.
    private Quote()
    {
    }

    public Guid RequestId { get; private set; }

    public Guid VendorOrganizationId { get; private set; }

    public QuoteStatus Status { get; private set; }

    public Money Amount { get; private set; }

    /// <summary>
    /// When the offer lapses, or null if it does not.
    /// </summary>
    /// <remarks>
    /// Stored rather than swept by a background service: the dashboard projections read it to
    /// surface proximity to expiry, so expiry is equally visible without a hosted service and
    /// its attendant transactional and idempotency questions.
    /// </remarks>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the quote last changed state, which is what "age in current state" is measured from.
    /// </summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>
    /// Why the quote reached its current state, when that was not the actor's direct intent.
    /// </summary>
    public string? StatusReason { get; private set; }

    internal static Quote Draft(
        Guid requestId,
        Guid vendorOrganizationId,
        Money amount,
        DateTimeOffset? expiresAt,
        string? notes,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), requestId, vendorOrganizationId, amount, expiresAt, notes, now);

    internal void ApplyStatus(QuoteStatus status, DateTimeOffset now, string? reason = null)
    {
        Status = status;
        StatusChangedAt = now;
        StatusReason = reason;
        Touch();
    }

    internal void ApplyEdit(Money amount, DateTimeOffset? expiresAt, string? notes)
    {
        if (!QuoteTransitions.IsEditable(Status))
        {
            throw new QuoteNotEditableException(new QuoteStatusName(Status.ToString()));
        }

        Amount = amount;
        ExpiresAt = expiresAt;
        Notes = notes;
        Touch();
    }
}

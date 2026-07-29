namespace QuoteManager.Domain.Requests;

/// <summary>
/// A vendor organization invited to quote on a request.
/// </summary>
/// <remarks>
/// This is the genuine many-to-many in the model: one request invites many vendors, and one vendor
/// is invited to many requests. It is deliberately separate from the request's own client
/// organization, which is a one-to-many and belongs on the request as a foreign key.
///
/// Its purpose is triage. Without it the dashboard can only report the quotes that arrived; with
/// it, the more useful fact — that two of five invited vendors have not responded — becomes
/// answerable, which is what lets a user act on silence rather than only on activity.
/// </remarks>
public sealed class RequestInvitation
{
    internal RequestInvitation(Guid requestId, Guid vendorOrganizationId, DateTimeOffset invitedAt)
    {
        RequestId = requestId;
        VendorOrganizationId = vendorOrganizationId;
        InvitedAt = invitedAt;
    }

    // EF Core materialisation.
    private RequestInvitation()
    {
    }

    public Guid RequestId { get; private set; }

    public Guid VendorOrganizationId { get; private set; }

    public DateTimeOffset InvitedAt { get; private set; }
}

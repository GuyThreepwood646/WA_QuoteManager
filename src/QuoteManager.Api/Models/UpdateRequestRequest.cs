using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/requests/{requestId}</c>.
/// </summary>
/// <remarks>
/// <c>clientOrganizationId</c> is deliberately absent - it isn't editable once a request exists,
/// mirroring <c>Quote.VendorOrganizationId</c>'s immutability.
/// </remarks>
public sealed record UpdateRequestRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    public DateTimeOffset? NeededBy { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/organizations/{organizationId}</c>.
/// </summary>
/// <remarks>
/// Only <c>Name</c> is editable - <c>Kind</c> is immutable once set, since flipping client/vendor
/// after a request or quote already references the organization would silently invalidate what
/// those records depend on.
/// </remarks>
public sealed record UpdateOrganizationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }
}

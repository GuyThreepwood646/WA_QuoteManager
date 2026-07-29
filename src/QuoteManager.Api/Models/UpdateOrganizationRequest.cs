using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/organizations/{organizationId}</c>.
/// </summary>
public sealed record UpdateOrganizationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }
}

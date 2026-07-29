using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// A location entry on create/update organization requests.
/// </summary>
public sealed record OrganizationLocationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Address { get; init; }

    [StringLength(50)]
    public string? Phone { get; init; }
}

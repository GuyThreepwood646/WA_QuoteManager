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

    [StringLength(500)]
    public string? PrimaryAddress { get; init; }

    [StringLength(200)]
    public string? PrimaryContactName { get; init; }

    [StringLength(320)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; init; }

    [StringLength(50)]
    public string? PrimaryContactPhone { get; init; }

    public bool IsPreferredVendor { get; init; }

    public OrganizationLocationRequest[] Locations { get; init; } = [];
}

using System.ComponentModel.DataAnnotations;
using QuoteManager.Domain.Identity;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>PUT /api/users/{userId}</c>. <c>Roles</c>/<c>OrganizationId</c> are always
/// present, but the endpoint refuses any change to them from a non-admin caller - even editing
/// their own account - so a non-admin's client sends back the same values it was given.
/// </summary>
public sealed record UpdateUserRequest : IValidatableObject
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(50)]
    public string? Phone { get; init; }

    public required string[] Roles { get; init; }

    public Guid? OrganizationId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AppRoleExtensions.TryParseRoles(Roles, out var parsedRoles))
        {
            yield return new ValidationResult(
                "roles must be a non-empty list of Requester, Reviewer, Vendor, and/or Admin.",
                [nameof(Roles)]);
        }
        else if (parsedRoles != AppRole.Admin && OrganizationId is null)
        {
            yield return new ValidationResult(
                "organizationId is required unless the only role is Admin.",
                [nameof(OrganizationId)]);
        }
    }
}

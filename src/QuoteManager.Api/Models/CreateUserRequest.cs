using System.ComponentModel.DataAnnotations;
using QuoteManager.Domain.Identity;

namespace QuoteManager.Api.Models;

/// <summary>
/// The body of <c>POST /api/users</c>.
/// </summary>
public sealed record CreateUserRequest : IValidatableObject
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    public required string[] Roles { get; init; }

    public Guid? OrganizationId { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(50)]
    public string? Phone { get; init; }

    [Required]
    public required string Password { get; init; }

    [Required]
    public required string ConfirmPassword { get; init; }

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

        if (Password != ConfirmPassword)
        {
            yield return new ValidationResult("Passwords do not match.", [nameof(ConfirmPassword)]);
        }

        foreach (var failure in PasswordPolicy.Evaluate(Password))
        {
            yield return new ValidationResult(failure, [nameof(Password)]);
        }
    }
}

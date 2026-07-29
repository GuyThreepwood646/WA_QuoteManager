using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;

namespace QuoteManager.Infrastructure.Identity;

/// <summary>
/// The <see cref="ICurrentUser"/> port, implemented over the authenticated principal.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => Guid.Parse(RequireClaim(ClaimTypes.NameIdentifier));

    public string DisplayName => RequireClaim(ClaimTypes.Name);

    public AppRole Roles
    {
        get
        {
            var roles = AppRole.None;
            foreach (var claim in Principal?.FindAll(ClaimTypes.Role) ?? [])
            {
                roles |= Enum.Parse<AppRole>(claim.Value);
            }

            return roles;
        }
    }

    public Guid? OrganizationId
    {
        get
        {
            var value = Principal?.FindFirst(AppClaimTypes.OrganizationId)?.Value;
            return value is null ? null : Guid.Parse(value);
        }
    }

    public DomainActor ToActor() => new(UserId, DisplayName, Roles, OrganizationId);

    private string RequireClaim(string type) =>
        Principal?.FindFirst(type)?.Value
        ?? throw new InvalidOperationException(
            $"No authenticated principal is available, or it is missing the '{type}' claim.");
}

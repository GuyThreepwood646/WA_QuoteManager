using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuoteManager.Domain.Identity;
using QuoteManager.Infrastructure.Identity;

namespace QuoteManager.Api.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues the AD-9 bearer token for an authenticated <see cref="AppUser"/>.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public IssuedToken IssueFor(AppUser user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(jwt.Lifetime);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Email, user.Email),
        ];

        claims.AddRange(user.Roles.Split().Select(role => new Claim(ClaimTypes.Role, role.ToString())));

        if (user.OrganizationId is { } organizationId)
        {
            claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationId.ToString()));
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

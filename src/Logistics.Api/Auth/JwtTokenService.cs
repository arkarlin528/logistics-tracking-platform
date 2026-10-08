using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Logistics.Application.Common;
using Logistics.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Logistics.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "logistics-api";
    public string Audience { get; set; } = "logistics-web";

    /// <summary>At least 32 characters. Comes from configuration / environment, never from source control.</summary>
    public string SigningKey { get; set; } = "";

    public int ExpiryMinutes { get; set; } = 120;

    public SymmetricSecurityKey SecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class AppClaims
{
    public const string CustomerId = "customer_id";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (user.CustomerId is { } customerId)
            claims.Add(new Claim(AppClaims.CustomerId, customerId.ToString()));

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(jwt.SecurityKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

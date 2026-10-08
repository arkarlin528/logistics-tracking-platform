using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Logistics.Application.Common;
using Logistics.Domain.Users;

namespace Logistics.Api.Auth;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId => ParseInt(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                   ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier));

    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role)
        ? role
        : null;

    public int? CustomerId => ParseInt(Principal?.FindFirstValue(AppClaims.CustomerId));

    private static int? ParseInt(string? value) => int.TryParse(value, out var result) ? result : null;
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Console.Api.Common;

namespace Console.Api.Features.Auth;

public static class CurrentUser
{
    public static Guid Id(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new ApiException(ErrorCodes.Unauthorized);

    public static string Actor(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email) ?? "unknown";
}

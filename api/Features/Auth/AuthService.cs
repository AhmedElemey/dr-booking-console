using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Console.Api.Features.Auth;

public sealed class AuthService(ConsoleDbContext db, IOptions<JwtOptions> jwt, TimeProvider clock)
{
    private static readonly PasswordHasher<SuperAdmin> Hasher = new();

    public static string HashPassword(string password) => Hasher.HashPassword(null!, password);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == normalized, ct);
        if (admin is null || !admin.IsActive || Hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new ApiException(ErrorCodes.InvalidCredentials);
        }

        var expires = clock.GetUtcNow().AddHours(jwt.Value.ExpiresHours);
        return new LoginResponse(IssueToken(admin, expires), expires, AdminDto.From(admin));
    }

    private string IssueToken(SuperAdmin admin, DateTimeOffset expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key));
        var token = new JwtSecurityToken(
            issuer: jwt.Value.Issuer,
            audience: jwt.Value.Issuer,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            ],
            notBefore: clock.GetUtcNow().UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, AdminDto Admin);

public sealed record AdminDto(Guid Id, string Email, string DisplayName, bool IsActive, DateTimeOffset CreatedAt)
{
    public static AdminDto From(SuperAdmin a) => new(a.Id, a.Email, a.DisplayName, a.IsActive, a.CreatedAt);
}

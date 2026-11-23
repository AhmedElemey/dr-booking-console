using System.Net.Mail;
using System.Security.Claims;

using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;
using Console.Api.Features.Audit;

using Microsoft.EntityFrameworkCore;

namespace Console.Api.Features.Auth;

public sealed record CreateAdminRequest(string? Email, string? Password, string? DisplayName);

public static class AuthEndpoints
{
    public static void MapAuth(this RouteGroupBuilder api)
    {
        api.MapPost("auth/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                errors["email"] = ["Email is required."];
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                errors["password"] = ["Password is required."];
            }

            if (errors.Count > 0)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, errors: errors);
            }

            return await auth.LoginAsync(request.Email!, request.Password!, ct);
        }).AllowAnonymous().RequireRateLimiting("console-login").WithTags("Auth");

        api.MapGet("me", async (ClaimsPrincipal user, ConsoleDbContext db, CancellationToken ct) =>
        {
            var admin = await db.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.Id == user.Id(), ct)
                ?? throw new ApiException(ErrorCodes.Unauthorized);
            return AdminDto.From(admin);
        }).WithTags("Auth");

        var admins = api.MapGroup("admins").WithTags("Super admins");

        admins.MapGet("", async (ConsoleDbContext db, CancellationToken ct) =>
            (await db.Admins.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync(ct)).Select(AdminDto.From).ToList());

        admins.MapPost("", async (CreateAdminRequest request, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            var email = (request.Email ?? "").Trim();
            if (!IsEmail(email))
            {
                errors["email"] = ["Enter a valid email address."];
            }

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 10)
            {
                errors["password"] = ["Password must be at least 10 characters."];
            }

            if (errors.Count > 0)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, errors: errors);
            }

            var normalized = AuthService.NormalizeEmail(email);
            if (await db.Admins.AnyAsync(a => a.Email == normalized, ct))
            {
                throw new ApiException(ErrorCodes.EmailTaken);
            }

            var admin = new SuperAdmin
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? normalized : request.DisplayName.Trim(),
                PasswordHash = AuthService.HashPassword(request.Password!),
                IsActive = true,
                CreatedAt = clock.GetUtcNow(),
            };
            db.Admins.Add(admin);
            audit.Add(user.Actor(), "admin.created", null, new { admin.Email });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/admins/{admin.Id}", AdminDto.From(admin));
        });

        admins.MapPost("{id:guid}/deactivate", async (Guid id, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, CancellationToken ct) =>
        {
            var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw ApiException.NotFound();
            if (admin.Id == user.Id() || (admin.IsActive && await db.Admins.CountAsync(a => a.IsActive, ct) <= 1))
            {
                throw new ApiException(ErrorCodes.LastAdmin);
            }

            admin.IsActive = false;
            audit.Add(user.Actor(), "admin.deactivated", null, new { admin.Email });
            await db.SaveChangesAsync(ct);
            return AdminDto.From(admin);
        });

        admins.MapPost("{id:guid}/activate", async (Guid id, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, CancellationToken ct) =>
        {
            var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw ApiException.NotFound();
            admin.IsActive = true;
            audit.Add(user.Actor(), "admin.activated", null, new { admin.Email });
            await db.SaveChangesAsync(ct);
            return AdminDto.From(admin);
        });
    }

    private static bool IsEmail(string value) =>
        value.Length is > 3 and <= 254 && MailAddress.TryCreate(value, out var m) && m.Address == value && value.Contains('.', StringComparison.Ordinal);
}

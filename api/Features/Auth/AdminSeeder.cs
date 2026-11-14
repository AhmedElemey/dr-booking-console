using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Console.Api.Features.Auth;

public static class AdminSeeder
{
    // Seeds the first super admin from SuperAdmin__Email / SuperAdmin__Password when none exists. Never logs the password.
    public static async Task SeedAsync(ConsoleDbContext db, IOptions<SuperAdminSeedOptions> options, TimeProvider clock, ILogger logger)
    {
        if (await db.Admins.AnyAsync())
        {
            return;
        }

        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrEmpty(seed.Password))
        {
            logger.LogWarning("No super admin exists and SuperAdmin__Email / SuperAdmin__Password are not set; nobody can sign in.");
            return;
        }

        var email = AuthService.NormalizeEmail(seed.Email);
        db.Admins.Add(new SuperAdmin
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = email,
            PasswordHash = AuthService.HashPassword(seed.Password),
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded the first super admin {Email}.", email);
    }
}

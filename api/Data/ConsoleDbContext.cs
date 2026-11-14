using Console.Api.Domain;

using Microsoft.EntityFrameworkCore;

namespace Console.Api.Data;

public sealed class ConsoleDbContext(DbContextOptions<ConsoleDbContext> options) : DbContext(options)
{
    public DbSet<SuperAdmin> Admins => Set<SuperAdmin>();

    public DbSet<Clinic> Clinics => Set<Clinic>();

    public DbSet<ClinicFeature> ClinicFeatures => Set<ClinicFeature>();

    public DbSet<ClinicRepo> ClinicRepos => Set<ClinicRepo>();

    public DbSet<ProvisioningJob> Jobs => Set<ProvisioningJob>();

    public DbSet<AuditEntry> Audit => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SuperAdmin>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.DisplayName).HasMaxLength(100);
        });

        b.Entity<Clinic>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(40);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ApiKeyPrefix).HasMaxLength(16);
            e.Property(x => x.ApiKeyHash).HasMaxLength(64);
            e.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Repos).WithOne().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Jobs).WithOne(x => x.Clinic).HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClinicFeature>(e =>
        {
            e.HasKey(x => new { x.ClinicId, x.Key });
            e.Property(x => x.Key).HasMaxLength(64);
        });

        b.Entity<ClinicRepo>(e =>
        {
            e.HasKey(x => new { x.ClinicId, x.Kind });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<ProvisioningJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.StepsJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ClinicId, x.CreatedAt });
        });

        b.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.At);
            e.HasIndex(x => x.ClinicId);
        });
    }
}

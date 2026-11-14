using System.Security.Claims;
using System.Text.Json;

using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;
using Console.Api.Features.Audit;
using Console.Api.Features.Auth;
using Console.Api.Features.Catalog;
using Console.Api.Features.Provisioning;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Console.Api.Features.Clinics;

public static class ClinicEndpoints
{
    public static void MapClinics(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("clinics").WithTags("Clinics");

        g.MapGet("", async (ConsoleDbContext db, string? search, ClinicStatus? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (p, size) = Paging.Normalize(page, pageSize);
            var query = db.Clinics.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
                query = query.Where(c => EF.Functions.ILike(c.Slug, term) || EF.Functions.ILike(c.NameEn, term) || EF.Functions.ILike(c.NameAr, term));
            }

            if (status is { } s)
            {
                query = query.Where(c => c.Status == s);
            }

            var total = await query.CountAsync(ct);
            var items = await query.Include(c => c.Features).Include(c => c.Repos)
                .OrderByDescending(c => c.CreatedAt).Skip((p - 1) * size).Take(size).AsSplitQuery().ToListAsync(ct);
            return new PagedList<ClinicSummaryDto>(items.Select(c => c.ToSummary()).ToList(), total, p, size);
        });

        // Validates a would-be clinic and returns the config and repos provisioning would produce (used by the wizard's review step).
        g.MapPost("preview", async (ClinicRequest request, ConsoleDbContext db, IOptions<ConsoleOptions> console, CancellationToken ct) =>
        {
            var req = ClinicValidation.Normalize(request);
            var features = ClinicValidation.ResolveFeatures(req.Features);
            if (await db.Clinics.AnyAsync(c => c.Slug == req.Slug, ct))
            {
                throw new ApiException(ErrorCodes.SlugTaken);
            }

            var clinic = FromRequest(req, Guid.Empty, DateTimeOffset.UnixEpoch);
            var config = ClinicConfigBuilder.Build(clinic, features, console.Value.PublicUrl, "(assigned when the clinic is created)");
            var repos = ClinicConfigBuilder.RequiredRepos(features).Select(k => ClinicConfigBuilder.RepoName(req.Slug!, k)).ToList();
            return new ConfigPreviewDto(JsonDocument.Parse(config).RootElement.Clone(), repos);
        });

        g.MapPost("", async (ClinicRequest request, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
        {
            var req = ClinicValidation.Normalize(request);
            var features = ClinicValidation.ResolveFeatures(req.Features);
            var now = clock.GetUtcNow();
            var actor = user.Actor();
            var clinic = FromRequest(req, Guid.NewGuid(), now);
            foreach (var (key, enabled) in features)
            {
                clinic.Features.Add(new ClinicFeature { ClinicId = clinic.Id, Key = key, Enabled = enabled, UpdatedAt = now, UpdatedBy = actor });
            }

            db.Clinics.Add(clinic);
            audit.Add(actor, "clinic.created", clinic.Id, new { clinic.Slug, features });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                throw new ApiException(ErrorCodes.SlugTaken);
            }

            return Results.Created($"/api/v1/clinics/{clinic.Id}", clinic.ToDto(null));
        });

        g.MapGet("{id:guid}", async (Guid id, ConsoleDbContext db, CancellationToken ct) =>
        {
            var clinic = await Load(db, id, ct);
            var latest = await db.Jobs.AsNoTracking().Where(j => j.ClinicId == id).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(ct);
            return clinic.ToDto(latest);
        });

        g.MapPatch("{id:guid}", async (Guid id, ClinicPatch patch, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
        {
            var p = ClinicValidation.Normalize(patch);
            var clinic = await Load(db, id, ct);
            var changed = new List<string>();
            void Set(string name, string? value, Action<string> apply)
            {
                if (value is not null)
                {
                    apply(value);
                    changed.Add(name);
                }
            }

            Set("nameAr", p.NameAr, v => clinic.NameAr = v);
            Set("nameEn", p.NameEn, v => clinic.NameEn = v);
            Set("taglineAr", p.TaglineAr, v => clinic.TaglineAr = v);
            Set("taglineEn", p.TaglineEn, v => clinic.TaglineEn = v);
            Set("primaryColor", p.PrimaryColor, v => clinic.PrimaryColor = v);
            Set("accentColor", p.AccentColor, v => clinic.AccentColor = v);
            Set("supportPhone", p.SupportPhone, v => clinic.SupportPhone = v);
            Set("domain", p.Domain, v => clinic.Domain = v);
            Set("androidApplicationId", p.AndroidApplicationId, v => clinic.AndroidApplicationId = v);
            Set("iosBundleId", p.IosBundleId, v => clinic.IosBundleId = v);
            Set("logoSvg", p.LogoSvg, v => clinic.LogoSvg = v.Length == 0 ? null : v);
            clinic.UpdatedAt = clock.GetUtcNow();
            audit.Add(user.Actor(), "clinic.updated", id, new { fields = changed });
            await db.SaveChangesAsync(ct);
            var latest = await db.Jobs.AsNoTracking().Where(j => j.ClinicId == id).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(ct);
            return clinic.ToDto(latest);
        });

        g.MapPost("{id:guid}/suspend", (Guid id, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
            SetSuspended(id, true, user, db, audit, clock, ct));

        g.MapPost("{id:guid}/activate", (Guid id, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
            SetSuspended(id, false, user, db, audit, clock, ct));

        g.MapPut("{id:guid}/features/{key}", async (Guid id, string key, SetFeatureRequest request, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
        {
            var def = FeatureCatalog.Find(key) ?? throw ApiException.NotFound(ErrorCodes.FeatureNotFound);
            if (!def.Available && request.Enabled)
            {
                throw new ApiException(ErrorCodes.FeatureNotAvailable, extensions: new Dictionary<string, object?> { ["feature"] = key });
            }

            var clinic = await Load(db, id, ct);
            var now = clock.GetUtcNow();
            var row = clinic.Features.FirstOrDefault(f => f.Key == key);
            var before = row?.Enabled ?? def.DefaultEnabled;
            if (row is null)
            {
                row = new ClinicFeature { ClinicId = id, Key = key };
                db.ClinicFeatures.Add(row);
                clinic.Features.Add(row);
            }

            row.Enabled = request.Enabled;
            row.UpdatedAt = now;
            row.UpdatedBy = user.Actor();
            clinic.UpdatedAt = now;
            audit.Add(user.Actor(), "feature.changed", id, new { key, from = before, to = request.Enabled });
            await db.SaveChangesAsync(ct);
            return clinic.FeatureStates().First(f => f.Key == key);
        });

        g.MapPost("{id:guid}/provision", async (Guid id, ProvisionRequest? request, ClaimsPrincipal user, ConsoleDbContext db, IProvisionerFactory factory, ProvisioningQueue queue, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
            Results.Accepted($"/api/v1/clinics/{id}/jobs", await StartJob(id, request?.DryRun ?? true, "provision.requested", user, db, factory, queue, audit, clock, ct)));

        // Re-commits clinic.config.json (and logo) to the clinic's repos after a brand change; same idempotent engine.
        g.MapPost("{id:guid}/sync-config", async (Guid id, ClaimsPrincipal user, ConsoleDbContext db, IProvisionerFactory factory, ProvisioningQueue queue, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
            Results.Accepted($"/api/v1/clinics/{id}/jobs", await StartJob(id, false, "config.sync.requested", user, db, factory, queue, audit, clock, ct, requireActive: true)));

        g.MapGet("{id:guid}/jobs", async (Guid id, ConsoleDbContext db, CancellationToken ct) =>
        {
            if (!await db.Clinics.AnyAsync(c => c.Id == id, ct))
            {
                throw ApiException.NotFound(ErrorCodes.ClinicNotFound);
            }

            return (await db.Jobs.AsNoTracking().Where(j => j.ClinicId == id).OrderByDescending(j => j.CreatedAt).Take(50).ToListAsync(ct)).Select(JobDto.From).ToList();
        });

        api.MapGet("jobs/{id:guid}", async (Guid id, ConsoleDbContext db, CancellationToken ct) =>
            JobDto.From(await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw ApiException.NotFound(ErrorCodes.JobNotFound))).WithTags("Clinics");

        g.MapPost("{id:guid}/api-key/rotate", async (Guid id, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw ApiException.NotFound(ErrorCodes.ClinicNotFound);
            var (key, prefix, hash) = ApiKeys.Generate();
            clinic.ApiKeyHash = hash;
            clinic.ApiKeyPrefix = prefix;
            clinic.UpdatedAt = clock.GetUtcNow();
            audit.Add(user.Actor(), "api-key.rotated", id, new { prefix });
            await db.SaveChangesAsync(ct);
            return new RotateKeyResponse(key, prefix);
        });

        g.MapGet("{id:guid}/config-preview", async (Guid id, ConsoleDbContext db, IOptions<ConsoleOptions> console, CancellationToken ct) =>
        {
            var clinic = await Load(db, id, ct);
            var features = clinic.FeatureStates().ToDictionary(f => f.Key, f => f.Enabled);
            return Results.Text(ClinicConfigBuilder.Build(clinic, features, console.Value.PublicUrl, clinic.Id.ToString()), "application/json");
        });

        // Anonymous: clinic backends authenticate with their API key instead of a super admin token.
        g.MapGet("{id:guid}/runtime-config", async (Guid id, HttpRequest http, ConsoleDbContext db, CancellationToken ct) =>
        {
            var clinic = await db.Clinics.AsNoTracking().Include(c => c.Features).FirstOrDefaultAsync(c => c.Id == id, ct);
            var presented = http.Headers["X-Clinic-Key"].FirstOrDefault();
            // Hash and compare even when the clinic is missing so response time does not reveal which ids exist.
            var matches = ApiKeys.Matches(presented, clinic?.ApiKeyHash ?? new string('0', 64));
            if (clinic is null || clinic.ApiKeyHash is null || !matches)
            {
                throw new ApiException(ErrorCodes.Unauthorized);
            }

            var suspended = clinic.Status == ClinicStatus.Suspended;
            var states = clinic.FeatureStates().Where(f => FeatureCatalog.Find(f.Key)!.Available);
            var features = states.ToDictionary(f => f.Key, f => !suspended && f.Enabled);
            var updatedAt = clinic.Features.Select(f => f.UpdatedAt).Append(clinic.UpdatedAt).Max();
            return new RuntimeConfigDto(features, updatedAt);
        }).AllowAnonymous();
    }

    private static Clinic FromRequest(ClinicRequest r, Guid id, DateTimeOffset now) =>
        new()
        {
            Id = id,
            Slug = r.Slug!,
            NameAr = r.NameAr!,
            NameEn = r.NameEn!,
            TaglineAr = r.TaglineAr ?? string.Empty,
            TaglineEn = r.TaglineEn ?? string.Empty,
            PrimaryColor = r.PrimaryColor!,
            AccentColor = r.AccentColor!,
            SupportPhone = r.SupportPhone!,
            Domain = r.Domain ?? string.Empty,
            AndroidApplicationId = r.AndroidApplicationId!,
            IosBundleId = r.IosBundleId!,
            LogoSvg = r.LogoSvg,
            Status = ClinicStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static async Task<Clinic> Load(ConsoleDbContext db, Guid id, CancellationToken ct) =>
        await db.Clinics.Include(c => c.Features).Include(c => c.Repos).AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound(ErrorCodes.ClinicNotFound);

    private static async Task<ClinicDto> SetSuspended(Guid id, bool suspended, ClaimsPrincipal user, ConsoleDbContext db, AuditLog audit, TimeProvider clock, CancellationToken ct)
    {
        var clinic = await Load(db, id, ct);
        if (suspended)
        {
            clinic.Status = ClinicStatus.Suspended;
        }
        else if (clinic.Status == ClinicStatus.Suspended)
        {
            clinic.Status = clinic.Repos.Count > 0 ? ClinicStatus.Active : ClinicStatus.Draft;
        }

        clinic.UpdatedAt = clock.GetUtcNow();
        audit.Add(user.Actor(), suspended ? "clinic.suspended" : "clinic.activated", id);
        await db.SaveChangesAsync(ct);
        var latest = await db.Jobs.AsNoTracking().Where(j => j.ClinicId == id).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(ct);
        return clinic.ToDto(latest);
    }

    private static async Task<ProvisionResponse> StartJob(
        Guid id,
        bool dryRun,
        string auditAction,
        ClaimsPrincipal user,
        ConsoleDbContext db,
        IProvisionerFactory factory,
        ProvisioningQueue queue,
        AuditLog audit,
        TimeProvider clock,
        CancellationToken ct,
        bool requireActive = false)
    {
        var clinic = await Load(db, id, ct);
        if (clinic.Status == ClinicStatus.Suspended || (requireActive && clinic.Status != ClinicStatus.Active))
        {
            throw new ApiException(ErrorCodes.ClinicNotActive);
        }

        if (await db.Jobs.AnyAsync(j => j.ClinicId == id && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running), ct))
        {
            throw new ApiException(ErrorCodes.JobInProgress);
        }

        var effectiveDry = factory.IsDryRun(dryRun);
        var features = clinic.FeatureStates().ToDictionary(f => f.Key, f => f.Enabled);

        // The key is created here (not in the worker) so its plaintext can be returned exactly once in this response.
        string? plaintext = null;
        if (!effectiveDry && clinic.ApiKeyHash is null)
        {
            var (key, prefix, hash) = ApiKeys.Generate();
            clinic.ApiKeyHash = hash;
            clinic.ApiKeyPrefix = prefix;
            plaintext = key;
        }

        var job = new ProvisioningJob
        {
            Id = Guid.NewGuid(),
            ClinicId = id,
            DryRun = effectiveDry,
            Status = JobStatus.Queued,
            StartedBy = user.Actor(),
            CreatedAt = clock.GetUtcNow(),
        };
        job.SetSteps(ProvisioningEngine.Plan(features));
        db.Jobs.Add(job);
        audit.Add(user.Actor(), auditAction, id, new { jobId = job.Id, dryRun = effectiveDry, apiKeyGenerated = plaintext is not null });
        await db.SaveChangesAsync(ct);
        queue.Enqueue(job.Id);
        return new ProvisionResponse(JobDto.From(job), plaintext);
    }
}

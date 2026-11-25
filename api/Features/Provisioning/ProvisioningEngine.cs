using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;
using Console.Api.Features.Audit;
using Console.Api.Features.Clinics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Console.Api.Features.Provisioning;

public sealed class ProvisioningEngine(
    ConsoleDbContext db,
    IProvisionerFactory factory,
    IOptions<ConsoleOptions> console,
    AuditLog audit,
    TimeProvider clock,
    ILogger<ProvisioningEngine> logger)
{
    public const string ValidateStep = "validate";
    public const string FinalizeStep = "finalize";

    public static string RepoStep(RepoKind k) => $"repo:{k.ToString().ToLowerInvariant()}";

    public static string ConfigStep(RepoKind k) => $"config:{k.ToString().ToLowerInvariant()}";

    public static string VariablesStep(RepoKind k) => $"variables:{k.ToString().ToLowerInvariant()}";

    // The ordered step list a run will go through; shown as pending in the UI before the worker starts.
    public static List<JobStep> Plan(IReadOnlyDictionary<string, bool> features)
    {
        var steps = new List<JobStep> { new(ValidateStep, StepStatus.Pending, null, null) };
        foreach (var kind in ClinicConfigBuilder.RequiredRepos(features))
        {
            steps.Add(new JobStep(RepoStep(kind), StepStatus.Pending, null, null));
            steps.Add(new JobStep(ConfigStep(kind), StepStatus.Pending, null, null));
            steps.Add(new JobStep(VariablesStep(kind), StepStatus.Pending, null, null));
        }

        steps.Add(new JobStep(FinalizeStep, StepStatus.Pending, null, null));
        return steps;
    }

    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        var job = await db.Jobs.Include(j => j.Clinic).ThenInclude(c => c!.Features).Include(j => j.Clinic).ThenInclude(c => c!.Repos)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job?.Clinic is not { } clinic || job.Status != JobStatus.Queued)
        {
            return;
        }

        var dry = factory.IsDryRun(job.DryRun);
        var gh = factory.Create(dry);
        var features = clinic.FeatureStates().ToDictionary(f => f.Key, f => f.Enabled);
        var steps = job.GetSteps();

        job.Status = JobStatus.Running;
        job.StartedAt = clock.GetUtcNow();
        if (!dry)
        {
            clinic.Status = ClinicStatus.Provisioning;
        }

        await db.SaveChangesAsync(ct);

        async Task Mark(string name, StepStatus status, string? message)
        {
            var i = steps.FindIndex(s => s.Name == name);
            steps[i] = new JobStep(name, status, message, clock.GetUtcNow());
            job.SetSteps(steps);
            await db.SaveChangesAsync(ct);
        }

        var current = string.Empty;
        try
        {
            current = ValidateStep;
            await Mark(current, StepStatus.Running, null);
            var validation = await gh.ValidateAsync(ClinicConfigBuilder.RequiredRepos(features), ct);
            if (!ClinicValidation.IsValidSlug(clinic.Slug))
            {
                throw new ProvisioningException($"The clinic slug \"{clinic.Slug}\" is not valid.");
            }

            var keyNote = dry ? "Dry run: no API key is generated." : clinic.ApiKeyHash is null ? "Warning: this clinic has no API key; rotate one so its backend can pull features." : $"API key {clinic.ApiKeyPrefix}… is set.";
            await Mark(current, StepStatus.Succeeded, $"{validation.Message} {keyNote}");

            var config = ClinicConfigBuilder.Build(clinic, features, console.Value.PublicUrl, clinic.Id.ToString());
            foreach (var kind in ClinicConfigBuilder.RequiredRepos(features))
            {
                var repoName = ClinicConfigBuilder.RepoName(clinic.Slug, kind);

                current = RepoStep(kind);
                await Mark(current, StepStatus.Running, null);
                var recorded = clinic.Repos.FirstOrDefault(r => r.Kind == kind);
                if (recorded is not null && !dry)
                {
                    await Mark(current, StepStatus.Skipped, $"Repository {recorded.FullName} already created; skipping.");
                }
                else
                {
                    var repo = await gh.EnsureRepoAsync(kind, repoName, ct);
                    if (!dry)
                    {
                        var row = new ClinicRepo { ClinicId = clinic.Id, Kind = kind, FullName = repo.FullName, HtmlUrl = repo.HtmlUrl, CreatedAt = clock.GetUtcNow() };
                        db.ClinicRepos.Add(row);
                        clinic.Repos.Add(row);
                    }

                    await Mark(current, StepStatus.Succeeded, repo.Message);
                }

                current = ConfigStep(kind);
                await Mark(current, StepStatus.Running, null);
                var commit = await gh.CommitFileAsync(repoName, "clinic.config.json", config, $"Brand for {OneLine(clinic.NameEn)}", ct);
                var messages = new List<string> { commit.Message };
                var allSkipped = commit.Skipped;
                if (!string.IsNullOrWhiteSpace(clinic.LogoSvg))
                {
                    var logo = await gh.CommitFileAsync(repoName, ClinicConfigBuilder.LogoPath, clinic.LogoSvg.Trim() + "\n", $"Logo for {OneLine(clinic.NameEn)}", ct);
                    messages.Add(logo.Message);
                    allSkipped &= logo.Skipped;
                }

                await Mark(current, allSkipped ? StepStatus.Skipped : StepStatus.Succeeded, string.Join(" ", messages));

                current = VariablesStep(kind);
                await Mark(current, StepStatus.Running, null);
                var url = await gh.SetVariableAsync(repoName, "CONSOLE_URL", console.Value.PublicUrl, ct);
                var id = await gh.SetVariableAsync(repoName, "CLINIC_ID", clinic.Id.ToString(), ct);
                await Mark(current, StepStatus.Succeeded, $"{url.Message} {id.Message}");
            }

            current = FinalizeStep;
            await Mark(current, StepStatus.Running, null);
            string finalMessage;
            if (dry)
            {
                finalMessage = "Dry run finished. Nothing was created and the clinic status is unchanged.";
            }
            else
            {
                if (clinic.Status == ClinicStatus.Provisioning)
                {
                    clinic.Status = ClinicStatus.Active;
                }

                finalMessage = $"Provisioned {clinic.Repos.Count} repositories. The clinic is {clinic.Status}. "
                    + "Before go-live, set the ReverseProxy__* variables on each Render service (DEPLOYMENT.md, Forwarding behind Render), or rate limits key on the proxy IP.";
            }

            clinic.UpdatedAt = clock.GetUtcNow();
            job.Status = JobStatus.Succeeded;
            job.FinishedAt = clock.GetUtcNow();
            audit.Add("system", dry ? "provision.dry-run.succeeded" : "provision.succeeded", clinic.Id, new { jobId = job.Id });
            await Mark(current, StepStatus.Succeeded, finalMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var message = ex is ProvisioningException ? ex.Message : $"Unexpected error: {ex.GetType().Name}";
            if (ex is not ProvisioningException)
            {
                logger.LogError(ex, "Provisioning job {JobId} failed in step {Step}", job.Id, current);
            }

            job.Status = JobStatus.Failed;
            job.FinishedAt = clock.GetUtcNow();
            if (!dry)
            {
                clinic.Status = ClinicStatus.Failed;
            }

            clinic.UpdatedAt = clock.GetUtcNow();
            audit.Add("system", dry ? "provision.dry-run.failed" : "provision.failed", clinic.Id, new { jobId = job.Id, step = current, error = message });
            if (current.Length > 0)
            {
                await Mark(current, StepStatus.Failed, message);
            }
            else
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private static string OneLine(string value) => value.Replace('\n', ' ').Replace('\r', ' ');
}

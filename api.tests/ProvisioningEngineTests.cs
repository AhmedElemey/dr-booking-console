using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Domain;
using Console.Api.Features.Audit;
using Console.Api.Features.Catalog;
using Console.Api.Features.Provisioning;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Console.Tests;

public sealed class ProvisioningEngineTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly GitHubOptions Gh = new()
    {
        Token = "ghp_test_token_value",
        Owner = "acme",
        TemplateOwner = "templates",
        PollIntervalMs = 1,
        ReadyTimeoutSeconds = 5,
    };

    private sealed class FixedFactory(IGitHubProvisioner real, IGitHubProvisioner dry) : IProvisionerFactory
    {
        public bool GitHubMode { get; set; } = true;

        public bool IsDryRun(bool requested) => requested || !GitHubMode;

        public IGitHubProvisioner Create(bool dryRun) => dryRun ? dry : real;
    }

    private (GitHubRestProvisioner Github, DryRunProvisioner Dry) Provisioners(FakeGitHub fake) =>
        (new GitHubRestProvisioner(Client(fake), Options.Create(Gh), TimeProvider.System), new DryRunProvisioner(Options.Create(Gh)));

    private static HttpClient Client(FakeGitHub fake)
    {
        var client = new HttpClient(fake);
        GitHubRestProvisioner.Configure(client, Gh);
        return client;
    }

    private async Task<(Guid ClinicId, Guid JobId)> SeedAsync(string slug, bool mobile = true, bool website = true, string? logo = null, bool dryRun = false)
    {
        await using var db = pg.NewDb();
        var now = DateTimeOffset.UtcNow;
        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            NameAr = "سمايل كير",
            NameEn = "Smile Care",
            TaglineAr = "ا",
            TaglineEn = "t",
            PrimaryColor = "#0F766E",
            AccentColor = "#F59E0B",
            SupportPhone = "+201000000000",
            Domain = "smilecare.example",
            AndroidApplicationId = "com.smilecare.app",
            IosBundleId = "com.smilecare.app",
            LogoSvg = logo,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var features = FeatureCatalog.Available.ToDictionary(f => f.Key, f => f.DefaultEnabled);
        features["mobileApp"] = mobile;
        features["website"] = website;
        foreach (var (k, v) in features)
        {
            clinic.Features.Add(new ClinicFeature { ClinicId = clinic.Id, Key = k, Enabled = v, UpdatedAt = now, UpdatedBy = "test" });
        }

        db.Clinics.Add(clinic);
        var job = await NewJob(db, clinic.Id, features, dryRun);
        return (clinic.Id, job);
    }

    private static async Task<Guid> NewJob(ConsoleDbContext db, Guid clinicId, Dictionary<string, bool> features, bool dryRun)
    {
        var job = new ProvisioningJob { Id = Guid.NewGuid(), ClinicId = clinicId, DryRun = dryRun, Status = JobStatus.Queued, StartedBy = "test", CreatedAt = DateTimeOffset.UtcNow };
        job.SetSteps(ProvisioningEngine.Plan(features));
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task RunAsync(Guid jobId, FakeGitHub fake, bool githubMode = true)
    {
        await using var db = pg.NewDb();
        var (rest, dry) = Provisioners(fake);
        var engine = new ProvisioningEngine(
            db,
            new FixedFactory(rest, dry) { GitHubMode = githubMode },
            Options.Create(new ConsoleOptions { PublicUrl = "https://console.test" }),
            new AuditLog(db, TimeProvider.System),
            TimeProvider.System,
            NullLogger<ProvisioningEngine>.Instance);
        await engine.RunAsync(jobId, CancellationToken.None);
    }

    [Fact]
    public async Task Full_run_makes_the_exact_github_calls_and_activates_the_clinic()
    {
        var (clinicId, jobId) = await SeedAsync("smile-a", mobile: false, website: false, logo: "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var fake = new FakeGitHub();
        await RunAsync(jobId, fake);

        Assert.Equal(
            [
                "GET repos/templates/dr-booking-backend/contents/clinic.config.json",
                "GET repos/templates/dr-booking-dashboard/contents/clinic.config.json",
                "POST repos/templates/dr-booking-backend/generate",
                "GET repos/acme/smile-a-backend",
                "GET repos/acme/smile-a-backend/contents/clinic.config.json",
                "GET repos/acme/smile-a-backend/contents/clinic.config.json",
                "PUT repos/acme/smile-a-backend/contents/clinic.config.json",
                "GET repos/acme/smile-a-backend/contents/branding/logo.svg",
                "PUT repos/acme/smile-a-backend/contents/branding/logo.svg",
                "POST repos/acme/smile-a-backend/actions/variables",
                "POST repos/acme/smile-a-backend/actions/variables",
                "POST repos/templates/dr-booking-dashboard/generate",
                "GET repos/acme/smile-a-dashboard",
                "GET repos/acme/smile-a-dashboard/contents/clinic.config.json",
                "GET repos/acme/smile-a-dashboard/contents/clinic.config.json",
                "PUT repos/acme/smile-a-dashboard/contents/clinic.config.json",
                "GET repos/acme/smile-a-dashboard/contents/branding/logo.svg",
                "PUT repos/acme/smile-a-dashboard/contents/branding/logo.svg",
                "POST repos/acme/smile-a-dashboard/actions/variables",
                "POST repos/acme/smile-a-dashboard/actions/variables",
            ],
            fake.Calls);
        Assert.Equal("Bearer ghp_test_token_value", fake.AuthorizationSeen);
        Assert.Equal("Brand for Smile Care", System.Text.Json.JsonDocument.Parse(fake.Bodies["PUT repos/acme/smile-a-backend/contents/clinic.config.json#1"]).RootElement.GetProperty("message").GetString());
        Assert.Contains("\"slug\": \"smile-a\"", fake.Repos["smile-a-backend"]["clinic.config.json"].Content);
        Assert.Contains($"\"clinicId\": \"{clinicId}\"", fake.Repos["smile-a-backend"]["clinic.config.json"].Content);
        foreach (var repo in new[] { "smile-a-backend", "smile-a-dashboard" })
        {
            var written = System.Text.Json.JsonDocument.Parse(fake.Repos[repo]["clinic.config.json"].Content).RootElement.GetProperty("features");
            Assert.Equal(FeatureCatalog.Available.Select(f => f.Key).Order().ToArray(), written.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.Equal(24, written.EnumerateObject().Count());
            Assert.False(written.GetProperty("onlinePayments").GetBoolean());
            Assert.False(written.GetProperty("videoConsultation").GetBoolean());
            Assert.True(written.GetProperty("guestBrowsing").GetBoolean());
        }

        Assert.Equal("https://console.test", fake.Variables["smile-a-backend"]["CONSOLE_URL"]);
        Assert.Equal(clinicId.ToString(), fake.Variables["smile-a-dashboard"]["CLINIC_ID"]);

        await using var db = pg.NewDb();
        var clinic = await db.Clinics.Include(c => c.Repos).SingleAsync(c => c.Id == clinicId);
        Assert.Equal(ClinicStatus.Active, clinic.Status);
        Assert.Equal([RepoKind.Backend, RepoKind.Dashboard], clinic.Repos.Select(r => r.Kind).Order().ToArray());
        Assert.Equal("https://github.com/acme/smile-a-backend", clinic.Repos.Single(r => r.Kind == RepoKind.Backend).HtmlUrl);
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.All(job.GetSteps(), s => Assert.Equal(StepStatus.Succeeded, s.Status));
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task Mobile_and_website_repos_are_created_only_when_their_switch_is_on()
    {
        var (clinicId, jobId) = await SeedAsync("smile-b");
        var fake = new FakeGitHub();
        await RunAsync(jobId, fake);
        Assert.Equal(["smile-b-backend", "smile-b-dashboard", "smile-b-mobile", "smile-b-website"], fake.Repos.Keys.Order().ToArray());
        Assert.Contains("POST repos/templates/dr-booking-mobile/generate", fake.Calls);
        Assert.Contains("POST repos/templates/dr-booking-website/generate", fake.Calls);
        await using var db = pg.NewDb();
        Assert.Equal(4, await db.ClinicRepos.CountAsync(r => r.ClinicId == clinicId));
    }

    [Fact]
    public async Task Failure_marks_job_and_clinic_failed_without_deleting_anything_and_a_retry_resumes()
    {
        var (clinicId, jobId) = await SeedAsync("smile-c", mobile: false, website: false);
        var fake = new FakeGitHub { FailWhen = r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath.Contains("smile-c-dashboard") };
        await RunAsync(jobId, fake);

        await using (var db = pg.NewDb())
        {
            var clinic = await db.Clinics.Include(c => c.Repos).SingleAsync(c => c.Id == clinicId);
            Assert.Equal(ClinicStatus.Failed, clinic.Status);
            var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
            Assert.Equal(JobStatus.Failed, job.Status);
            var failed = job.GetSteps().Single(s => s.Status == StepStatus.Failed);
            Assert.Equal("config:dashboard", failed.Name);
            Assert.Equal("GitHub 403: Resource not accessible by personal access token", failed.Message);
            Assert.Equal(StepStatus.Pending, job.GetSteps().Single(s => s.Name == "variables:dashboard").Status);
            // Both repos exist and were recorded; nothing was deleted.
            Assert.Equal(2, clinic.Repos.Count);
            Assert.DoesNotContain(fake.Calls, c => c.StartsWith("DELETE", StringComparison.Ordinal));
        }

        // Retry: the failure clears, finished work is skipped or reused, and the run completes.
        fake.FailWhen = null;
        fake.Calls.Clear();
        Guid retry;
        await using (var db = pg.NewDb())
        {
            var features = (await db.ClinicFeatures.Where(f => f.ClinicId == clinicId).ToListAsync()).ToDictionary(f => f.Key, f => f.Enabled);
            retry = await NewJob(db, clinicId, features, false);
        }

        await RunAsync(retry, fake);
        await using (var db = pg.NewDb())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == retry);
            Assert.Equal(JobStatus.Succeeded, job.Status);
            var steps = job.GetSteps().ToDictionary(s => s.Name);
            Assert.Equal(StepStatus.Skipped, steps["repo:backend"].Status);
            Assert.Equal(StepStatus.Skipped, steps["config:backend"].Status);
            Assert.Equal(StepStatus.Skipped, steps["repo:dashboard"].Status);
            Assert.Equal(StepStatus.Succeeded, steps["config:dashboard"].Status);
            Assert.Equal(ClinicStatus.Active, (await db.Clinics.SingleAsync(c => c.Id == clinicId)).Status);
        }

        Assert.DoesNotContain(fake.Calls, c => c.Contains("/generate", StringComparison.Ordinal));
        Assert.DoesNotContain("PUT repos/acme/smile-c-backend/contents/clinic.config.json", fake.Calls);
        // Variables that already exist are updated, not duplicated.
        Assert.Contains("PATCH repos/acme/smile-c-backend/actions/variables/CONSOLE_URL", fake.Calls);
    }

    [Fact]
    public async Task A_repository_that_already_exists_on_github_is_reused()
    {
        var (_, jobId) = await SeedAsync("smile-d", mobile: false, website: false);
        var fake = new FakeGitHub();
        fake.Repos["smile-d-backend"] = new() { ["clinic.config.json"] = ("sha-old", "{}\n") };
        await RunAsync(jobId, fake);

        await using var db = pg.NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Contains("already existed", job.GetSteps().Single(s => s.Name == "repo:backend").Message);
        // The existing file is updated using its current sha.
        Assert.Contains("\"sha\":\"sha-old\"", fake.Bodies["PUT repos/acme/smile-d-backend/contents/clinic.config.json#1"]);
    }

    [Fact]
    public async Task Waits_until_the_template_copy_finishes()
    {
        var (_, jobId) = await SeedAsync("smile-e", mobile: false, website: false);
        var fake = new FakeGitHub { NotReadyPolls = 3 };
        await RunAsync(jobId, fake);
        await using var db = pg.NewDb();
        Assert.Equal(JobStatus.Succeeded, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
        Assert.True(fake.Calls.Count(c => c == "GET repos/acme/smile-e-backend/contents/clinic.config.json") >= 4);
    }

    [Fact]
    public async Task Times_out_with_a_clear_message_when_the_template_has_no_config_file()
    {
        var (_, jobId) = await SeedAsync("smile-f", mobile: false, website: false);
        var fake = new FakeGitHub { NotReadyPolls = int.MaxValue };
        Gh.ReadyTimeoutSeconds = 0;
        try
        {
            await RunAsync(jobId, fake);
        }
        finally
        {
            Gh.ReadyTimeoutSeconds = 5;
        }

        await using var db = pg.NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("Timed out waiting for clinic.config.json", job.GetSteps().Single(s => s.Status == StepStatus.Failed).Message);
    }

    [Fact]
    public async Task Dry_run_calls_nothing_records_every_step_and_leaves_the_clinic_untouched()
    {
        var (clinicId, jobId) = await SeedAsync("smile-g", dryRun: true);
        var fake = new FakeGitHub();
        await RunAsync(jobId, fake);

        Assert.Empty(fake.Calls);
        await using var db = pg.NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        var steps = job.GetSteps();
        Assert.All(steps, s => Assert.Equal(StepStatus.Succeeded, s.Status));
        Assert.Contains("Would create private repository", steps.Single(s => s.Name == "repo:mobile").Message);
        Assert.Equal(ClinicStatus.Draft, (await db.Clinics.SingleAsync(c => c.Id == clinicId)).Status);
        Assert.Empty(db.ClinicRepos.Where(r => r.ClinicId == clinicId));
    }

    [Fact]
    public async Task Server_not_in_github_mode_forces_dry_run()
    {
        var (_, jobId) = await SeedAsync("smile-h", mobile: false, website: false);
        var fake = new FakeGitHub();
        await RunAsync(jobId, fake, githubMode: false);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Missing_token_fails_in_the_validate_step_with_a_helpful_message()
    {
        var (_, jobId) = await SeedAsync("smile-i", mobile: false, website: false);
        var original = Gh.Token;
        Gh.Token = "";
        try
        {
            await RunAsync(jobId, new FakeGitHub());
        }
        finally
        {
            Gh.Token = original;
        }

        await using var db = pg.NewDb();
        var failed = (await db.Jobs.SingleAsync(j => j.Id == jobId)).GetSteps().Single(s => s.Status == StepStatus.Failed);
        Assert.Equal("validate", failed.Name);
        Assert.Contains("GitHub__Token", failed.Message);
    }

    [Fact]
    public async Task Validate_checks_every_template_in_use_and_only_those()
    {
        var (_, jobId) = await SeedAsync("smile-j", mobile: false, website: false);
        var fake = new FakeGitHub();
        await RunAsync(jobId, fake);

        var lookups = fake.Calls.Where(c => c.StartsWith("GET repos/templates/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            ["GET repos/templates/dr-booking-backend/contents/clinic.config.json", "GET repos/templates/dr-booking-dashboard/contents/clinic.config.json"],
            lookups);
    }

    [Fact]
    public async Task A_template_without_clinic_config_fails_validate_early_naming_the_template_and_its_default_branch()
    {
        var (clinicId, jobId) = await SeedAsync("smile-k", mobile: false, website: false);
        var fake = new FakeGitHub();
        fake.Templates["dr-booking-dashboard"] = ("develop", false);
        await RunAsync(jobId, fake);

        await using var db = pg.NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Failed, job.Status);
        var failed = job.GetSteps().Single(s => s.Status == StepStatus.Failed);
        Assert.Equal("validate", failed.Name);
        Assert.Contains("templates/dr-booking-dashboard", failed.Message);
        Assert.Contains("default branch (develop)", failed.Message);
        Assert.Contains("clinic.config.json", failed.Message);
        // Nothing was created before the failure.
        Assert.Empty(fake.Repos);
        Assert.DoesNotContain(fake.Calls, c => c.Contains("/generate", StringComparison.Ordinal));
        Assert.Equal(ClinicStatus.Failed, (await db.Clinics.SingleAsync(c => c.Id == clinicId)).Status);
    }

    [Fact]
    public async Task A_template_that_is_not_in_use_is_not_checked()
    {
        var (_, jobId) = await SeedAsync("smile-l", mobile: false, website: false);
        var fake = new FakeGitHub();
        fake.Templates["dr-booking-mobile"] = ("main", false);
        await RunAsync(jobId, fake);

        await using var db = pg.NewDb();
        Assert.Equal(JobStatus.Succeeded, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    [Fact]
    public async Task A_template_the_token_cannot_see_fails_validate_with_a_not_found_message()
    {
        var (_, jobId) = await SeedAsync("smile-m", mobile: false, website: false);
        var fake = new FakeGitHub();
        fake.Templates.Remove("dr-booking-backend");
        await RunAsync(jobId, fake);

        await using var db = pg.NewDb();
        var failed = (await db.Jobs.SingleAsync(j => j.Id == jobId)).GetSteps().Single(s => s.Status == StepStatus.Failed);
        Assert.Equal("validate", failed.Name);
        Assert.Contains("templates/dr-booking-backend was not found", failed.Message);
    }

    [Fact]
    public async Task An_existing_repository_without_clinic_config_fails_immediately_instead_of_timing_out()
    {
        var (_, jobId) = await SeedAsync("smile-n", mobile: false, website: false);
        var fake = new FakeGitHub();
        fake.Repos["smile-n-backend"] = new() { ["README.md"] = ("sha-readme", "hello\n") };
        var original = Gh.ReadyTimeoutSeconds;
        Gh.ReadyTimeoutSeconds = 60;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await RunAsync(jobId, fake);
        }
        finally
        {
            Gh.ReadyTimeoutSeconds = original;
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30));
        await using var db = pg.NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Failed, job.Status);
        var failed = job.GetSteps().Single(s => s.Status == StepStatus.Failed);
        Assert.Equal("repo:backend", failed.Name);
        Assert.Contains("acme/smile-n-backend already exists", failed.Message);
        Assert.Contains("not a copy of the current dr-booking-backend template", failed.Message);
        // Exactly one contents lookup: no polling.
        Assert.Equal(1, fake.Calls.Count(c => c == "GET repos/acme/smile-n-backend/contents/clinic.config.json"));
    }
}

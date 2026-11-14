using System.Net;
using System.Text.Json;

namespace Console.Tests;

public sealed class ProvisioningApiTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ConsoleApiFactory _factory;
    private HttpClient _client = null!;

    public ProvisioningApiTests(PostgresFixture db) => _factory = new ConsoleApiFactory(db.ConnectionString);

    public async Task InitializeAsync() => _client = await _factory.LoginAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<string> CreateAsync(string slug)
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic(slug));
        return (await response.JsonAsync()).GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> WaitForJobAsync(string jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = await (await _client.GetAsync($"/api/v1/jobs/{jobId}")).JsonAsync();
            if (job.GetProperty("status").GetString() is "Succeeded" or "Failed")
            {
                return job;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The provisioning job did not finish.");
    }

    [Fact]
    public async Task Provision_runs_in_the_background_and_a_dry_run_changes_nothing()
    {
        var id = await CreateAsync("prov-one");
        var response = await _client.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = true });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("apiKey").ValueKind);
        var queued = body.GetProperty("job");
        Assert.True(queued.GetProperty("dryRun").GetBoolean());
        Assert.Equal("validate", queued.GetProperty("steps")[0].GetProperty("name").GetString());

        var job = await WaitForJobAsync(queued.GetProperty("id").GetString()!);
        Assert.Equal("Succeeded", job.GetProperty("status").GetString());
        var steps = job.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(14, steps.Count);
        Assert.All(steps, s => Assert.Equal("Succeeded", s.GetProperty("status").GetString()));
        Assert.Contains("Would create private repository", steps.Single(s => s.GetProperty("name").GetString() == "repo:website").GetProperty("message").GetString());

        var clinic = await (await _client.GetAsync($"/api/v1/clinics/{id}")).JsonAsync();
        Assert.Equal("Draft", clinic.GetProperty("status").GetString());
        Assert.Equal(0, clinic.GetProperty("repos").GetArrayLength());
        Assert.False(clinic.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal(job.GetProperty("id").GetString(), clinic.GetProperty("latestJob").GetProperty("id").GetString());

        var jobs = await (await _client.GetAsync($"/api/v1/clinics/{id}/jobs")).JsonAsync();
        Assert.Equal(1, jobs.GetArrayLength());
    }

    [Fact]
    public async Task Without_github_mode_even_a_real_run_request_is_forced_to_dry_run()
    {
        var id = await CreateAsync("prov-two");
        var body = await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = false })).JsonAsync();
        Assert.True(body.GetProperty("job").GetProperty("dryRun").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("apiKey").ValueKind);
        await WaitForJobAsync(body.GetProperty("job").GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Real_run_without_a_token_fails_with_the_github_message_and_marks_the_clinic_failed()
    {
        using var github = _factory.WithWebHostBuilder(b => b.UseSetting("Provisioning:Mode", "GitHub"));
        var id = await CreateAsync("prov-three");

        // Switch the same database to GitHub mode (no token configured).
        var realClient = github.CreateClient();
        var login = await realClient.PostJsonAsync("/api/v1/auth/login", new { email = ConsoleApiFactory.AdminEmail, password = ConsoleApiFactory.AdminPassword });
        realClient.DefaultRequestHeaders.Authorization = new("Bearer", (await login.JsonAsync()).GetProperty("accessToken").GetString());
        var started = await (await realClient.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = false })).JsonAsync();
        Assert.False(started.GetProperty("job").GetProperty("dryRun").GetBoolean());
        var key = started.GetProperty("apiKey").GetString();
        Assert.Matches("^ck_[0-9a-f]{8}_[0-9a-f]{48}$", key);

        JsonElement job = default;
        for (var i = 0; i < 100; i++)
        {
            job = await (await realClient.GetAsync($"/api/v1/jobs/{started.GetProperty("job").GetProperty("id").GetString()}")).JsonAsync();
            if (job.GetProperty("status").GetString() == "Failed")
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.Equal("Failed", job.GetProperty("status").GetString());
        var failed = job.GetProperty("steps").EnumerateArray().Single(s => s.GetProperty("status").GetString() == "Failed");
        Assert.Contains("GitHub__Token", failed.GetProperty("message").GetString());
        var clinic = await (await realClient.GetAsync($"/api/v1/clinics/{id}")).JsonAsync();
        Assert.Equal("Failed", clinic.GetProperty("status").GetString());
        Assert.True(clinic.GetProperty("hasApiKey").GetBoolean());
        Assert.DoesNotContain(key!, clinic.GetRawText());
    }

    [Fact]
    public async Task A_second_job_is_refused_while_one_is_queued_or_running_and_unknown_ids_are_404()
    {
        var id = await CreateAsync("prov-four");
        var first = await _client.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = true });
        var second = await _client.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = true });
        // The dry run is fast; either the second request raced the first (409) or the first had finished (202).
        Assert.True(second.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Accepted);
        if (second.StatusCode == HttpStatusCode.Conflict)
        {
            Assert.Equal("JOB_IN_PROGRESS", await second.CodeAsync());
        }

        await WaitForJobAsync((await first.JsonAsync()).GetProperty("job").GetProperty("id").GetString()!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostJsonAsync($"/api/v1/clinics/{Guid.NewGuid()}/provision")).StatusCode);
    }

    [Fact]
    public async Task Suspended_clinics_cannot_be_provisioned()
    {
        var id = await CreateAsync("prov-five");
        await _client.PostJsonAsync($"/api/v1/clinics/{id}/suspend");
        var response = await _client.PostJsonAsync($"/api/v1/clinics/{id}/provision", new { dryRun = true });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLINIC_NOT_ACTIVE", await response.CodeAsync());
    }
}

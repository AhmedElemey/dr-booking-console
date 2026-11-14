using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Console.Api.Common;
using Console.Api.Domain;

using Microsoft.Extensions.Options;

namespace Console.Api.Features.Provisioning;

// Real provisioner over the GitHub REST API. The token only ever travels in the Authorization header.
public sealed class GitHubRestProvisioner(HttpClient http, IOptions<GitHubOptions> options, TimeProvider clock) : IGitHubProvisioner
{
    private readonly GitHubOptions _gh = options.Value;

    public static void Configure(HttpClient client, GitHubOptions gh)
    {
        client.BaseAddress = new Uri(gh.ApiBaseUrl.EndsWith('/') ? gh.ApiBaseUrl : gh.ApiBaseUrl + "/");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("clinic-console");
        if (!string.IsNullOrEmpty(gh.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gh.Token);
        }
    }

    public Task<StepOutcome> ValidateAsync(CancellationToken ct)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(_gh.Token))
        {
            missing.Add("GitHub__Token");
        }

        if (string.IsNullOrWhiteSpace(_gh.Owner))
        {
            missing.Add("GitHub__Owner");
        }

        return missing.Count > 0
            ? throw new ProvisioningException($"GitHub is not configured: set {string.Join(" and ", missing)}.")
            : Task.FromResult(new StepOutcome($"GitHub configured for owner {_gh.Owner}."));
    }

    public async Task<RepoOutcome> EnsureRepoAsync(RepoKind kind, string repoName, CancellationToken ct)
    {
        var template = DryRunProvisioner.TemplateName(_gh.Templates, kind);
        var templateOwner = _gh.TemplateOwnerOrOwner;
        var reused = false;

        using var create = await http.PostAsJsonAsync(
            $"repos/{templateOwner}/{template}/generate",
            new { owner = _gh.Owner, name = repoName, @private = true, include_all_branches = false },
            ct);
        if (create.StatusCode == HttpStatusCode.UnprocessableEntity && (await ErrorTextAsync(create, ct)).Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            reused = true;
        }
        else
        {
            await EnsureSuccessAsync(create, ct);
        }

        string htmlUrl;
        using (var repo = await http.GetAsync($"repos/{_gh.Owner}/{repoName}", ct))
        {
            if (repo.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await repo.Content.ReadAsStringAsync(ct));
                htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? $"https://github.com/{_gh.Owner}/{repoName}";
            }
            else if (reused)
            {
                await EnsureSuccessAsync(repo, ct);
                htmlUrl = string.Empty;
            }
            else
            {
                htmlUrl = $"https://github.com/{_gh.Owner}/{repoName}";
            }
        }

        await WaitForConfigAsync(repoName, ct);
        var message = reused ? $"Repository {_gh.Owner}/{repoName} already existed; reusing it." : $"Created private repository {_gh.Owner}/{repoName} from {templateOwner}/{template}.";
        return new RepoOutcome($"{_gh.Owner}/{repoName}", htmlUrl, reused, message);
    }

    public async Task<StepOutcome> CommitFileAsync(string repoName, string path, string content, string message, CancellationToken ct)
    {
        var url = $"repos/{_gh.Owner}/{repoName}/contents/{path}";
        string? sha = null;
        using (var existing = await http.GetAsync(url, ct))
        {
            if (existing.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await existing.Content.ReadAsStringAsync(ct));
                sha = doc.RootElement.GetProperty("sha").GetString();
                var current = DecodeContent(doc.RootElement);
                if (current == content)
                {
                    return new StepOutcome($"{path} is already up to date.", Skipped: true);
                }
            }
            else if (existing.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(existing, ct);
            }
        }

        var body = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
        };
        if (sha is not null)
        {
            body["sha"] = sha;
        }

        using var put = await http.PutAsJsonAsync(url, body, ct);
        await EnsureSuccessAsync(put, ct);
        return new StepOutcome($"Committed {path} to {_gh.Owner}/{repoName}.");
    }

    public async Task<StepOutcome> SetVariableAsync(string repoName, string name, string value, CancellationToken ct)
    {
        var baseUrl = $"repos/{_gh.Owner}/{repoName}/actions/variables";
        using var create = await http.PostAsJsonAsync(baseUrl, new { name, value }, ct);
        if (create.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            using var update = await http.PatchAsJsonAsync($"{baseUrl}/{name}", new { name, value }, ct);
            await EnsureSuccessAsync(update, ct);
            return new StepOutcome($"Updated Actions variable {name}.");
        }

        await EnsureSuccessAsync(create, ct);
        return new StepOutcome($"Created Actions variable {name}.");
    }

    private async Task WaitForConfigAsync(string repoName, CancellationToken ct)
    {
        var deadline = clock.GetUtcNow().AddSeconds(_gh.ReadyTimeoutSeconds);
        while (true)
        {
            using var response = await http.GetAsync($"repos/{_gh.Owner}/{repoName}/contents/clinic.config.json", ct);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(response, ct);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                throw new ProvisioningException($"Timed out waiting for clinic.config.json to appear in {_gh.Owner}/{repoName}. Check that the template repository has this file at its root.");
            }

            await Task.Delay(_gh.PollIntervalMs, ct);
        }
    }

    private static string DecodeContent(JsonElement element)
    {
        var raw = element.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace("\n", string.Empty, StringComparison.Ordinal)));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static async Task<string> ErrorTextAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(text);
            var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                message += " " + string.Join(" ", errors.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.TryGetProperty("message", out var em) ? em.GetString() : null).Where(s => s is not null));
            }

            return message?.Trim() ?? text;
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ProvisioningException($"GitHub {(int)response.StatusCode}: {await ErrorTextAsync(response, ct)}");
        }
    }
}

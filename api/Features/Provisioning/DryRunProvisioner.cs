using Console.Api.Common;
using Console.Api.Domain;

using Microsoft.Extensions.Options;

namespace Console.Api.Features.Provisioning;

// Records what a real run would do and touches nothing. Also the provisioner used in tests.
public sealed class DryRunProvisioner(IOptions<GitHubOptions> options) : IGitHubProvisioner
{
    private readonly GitHubOptions _gh = options.Value;

    public List<string> Calls { get; } = [];

    private string Owner => string.IsNullOrWhiteSpace(_gh.Owner) ? "<owner>" : _gh.Owner;

    public Task<StepOutcome> ValidateAsync(IReadOnlyCollection<RepoKind> kinds, CancellationToken ct) =>
        Task.FromResult(new StepOutcome("Dry run: nothing will be created on GitHub."));

    public Task<RepoOutcome> EnsureRepoAsync(RepoKind kind, string repoName, CancellationToken ct)
    {
        var template = TemplateName(_gh.Templates, kind);
        var message = $"Would create private repository {Owner}/{repoName} from template {(string.IsNullOrWhiteSpace(_gh.TemplateOwnerOrOwner) ? "<owner>" : _gh.TemplateOwnerOrOwner)}/{template}.";
        Calls.Add($"repo {Owner}/{repoName}");
        return Task.FromResult(new RepoOutcome($"{Owner}/{repoName}", $"https://github.com/{Owner}/{repoName}", false, message));
    }

    public Task<StepOutcome> CommitFileAsync(string repoName, string path, string content, string message, CancellationToken ct)
    {
        Calls.Add($"commit {repoName}/{path}");
        return Task.FromResult(new StepOutcome($"Would commit {path} ({content.Length} characters) to {Owner}/{repoName}: \"{message}\"."));
    }

    public Task<StepOutcome> SetVariableAsync(string repoName, string name, string value, CancellationToken ct)
    {
        Calls.Add($"variable {repoName}/{name}");
        return Task.FromResult(new StepOutcome($"Would set Actions variable {name} on {Owner}/{repoName}."));
    }

    public static string TemplateName(GitHubTemplates templates, RepoKind kind) => kind switch
    {
        RepoKind.Backend => templates.Backend,
        RepoKind.Dashboard => templates.Dashboard,
        RepoKind.Mobile => templates.Mobile,
        _ => templates.Website,
    };
}

using Console.Api.Domain;

namespace Console.Api.Features.Provisioning;

public sealed record RepoOutcome(string FullName, string HtmlUrl, bool Reused, string Message);

public sealed record StepOutcome(string Message, bool Skipped = false);

// The GitHub side of provisioning, one call per engine action. Implementations must be idempotent.
public interface IGitHubProvisioner
{
    // Fails with a readable message when the mode cannot work (for example the token is not configured, or a
    // template that this run will use has no clinic.config.json at the root of its default branch).
    Task<StepOutcome> ValidateAsync(IReadOnlyCollection<RepoKind> kinds, CancellationToken ct);

    // Creates {owner}/{repoName} from the template of `kind` (or reuses it) and waits until its files exist.
    Task<RepoOutcome> EnsureRepoAsync(RepoKind kind, string repoName, CancellationToken ct);

    // Creates or updates a file; reports Skipped when the repo already holds exactly this content.
    Task<StepOutcome> CommitFileAsync(string repoName, string path, string content, string message, CancellationToken ct);

    // Creates or updates a repository Actions variable.
    Task<StepOutcome> SetVariableAsync(string repoName, string name, string value, CancellationToken ct);
}

public interface IProvisionerFactory
{
    // A job runs as a dry run when it asked for one or when the server is not in GitHub mode.
    bool IsDryRun(bool requested);

    IGitHubProvisioner Create(bool dryRun);
}

public sealed class ProvisioningException(string message, Exception? inner = null) : Exception(message, inner);

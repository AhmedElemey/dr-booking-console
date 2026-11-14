using Console.Api.Common;

using Microsoft.Extensions.Options;

namespace Console.Api.Features.Provisioning;

public sealed class ProvisionerFactory(IServiceProvider services, IOptions<ProvisioningOptions> options) : IProvisionerFactory
{
    public bool IsDryRun(bool requested) => requested || !options.Value.UseGitHub;

    public IGitHubProvisioner Create(bool dryRun) =>
        dryRun
            ? services.GetRequiredService<DryRunProvisioner>()
            : services.GetRequiredService<GitHubRestProvisioner>();
}

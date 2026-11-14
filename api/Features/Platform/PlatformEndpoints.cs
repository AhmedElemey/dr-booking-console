using Console.Api.Common;

using Microsoft.Extensions.Options;

namespace Console.Api.Features.Platform;

// ProvisioningMode lets the UI explain that jobs are dry runs when the server is not in GitHub mode.
public sealed record PlatformDto(string Name, string ProvisioningMode);

public static class PlatformEndpoints
{
    public static void MapPlatform(this RouteGroupBuilder api) =>
        api.MapGet("platform", (IOptions<PlatformOptions> platform, IOptions<ProvisioningOptions> provisioning) =>
                new PlatformDto(platform.Value.Name, provisioning.Value.UseGitHub ? "GitHub" : "DryRun"))
            .AllowAnonymous()
            .WithTags("Platform");
}

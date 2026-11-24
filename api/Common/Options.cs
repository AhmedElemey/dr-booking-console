namespace Console.Api.Common;

public sealed class PlatformOptions
{
    public const string Section = "Platform";

    // The one place the product display name lives; surfaced to the web app through GET /api/v1/platform.
    public string Name { get; set; } = "Dr booking";
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Key { get; set; } = "";

    public string Issuer { get; set; } = "clinic-console";

    public int ExpiresHours { get; set; } = 8;
}

public sealed class SuperAdminSeedOptions
{
    public const string Section = "SuperAdmin";

    public string? Email { get; set; }

    public string? Password { get; set; }
}

public sealed class ConsoleOptions
{
    public const string Section = "Console";

    // Public origin of this console; written into every clinic's clinic.config.json.
    public string PublicUrl { get; set; } = "";
}

public sealed class ProvisioningOptions
{
    public const string Section = "Provisioning";

    // GitHub | DryRun. A job's dryRun flag forces DryRun regardless.
    public string Mode { get; set; } = "DryRun";

    public bool UseGitHub => string.Equals(Mode, "GitHub", StringComparison.OrdinalIgnoreCase);
}

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    public string Token { get; set; } = "";

    public string Owner { get; set; } = "";

    public string TemplateOwner { get; set; } = "";

    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    public GitHubTemplates Templates { get; set; } = new();

    public int PollIntervalMs { get; set; } = 3000;

    public int ReadyTimeoutSeconds { get; set; } = 120;

    public string TemplateOwnerOrOwner => string.IsNullOrWhiteSpace(TemplateOwner) ? Owner : TemplateOwner;
}

public sealed class GitHubTemplates
{
    public string Backend { get; set; } = "dr-booking-backend";

    public string Dashboard { get; set; } = "dr-booking-dashboard";

    public string Mobile { get; set; } = "dr-booking-mobile";

    public string Website { get; set; } = "dr-booking-website";
}

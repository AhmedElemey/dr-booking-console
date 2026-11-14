using System.Text.Json;

namespace Console.Api.Domain;

public enum ClinicStatus
{
    Draft,
    Provisioning,
    Active,
    Failed,
    Suspended,
}

public enum RepoKind
{
    Backend,
    Dashboard,
    Mobile,
    Website,
}

public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

public enum StepStatus
{
    Pending,
    Running,
    Succeeded,
    Skipped,
    Failed,
}

public sealed class SuperAdmin
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Clinic
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = "";

    public string NameAr { get; set; } = "";

    public string NameEn { get; set; } = "";

    public string TaglineAr { get; set; } = "";

    public string TaglineEn { get; set; } = "";

    public string PrimaryColor { get; set; } = "";

    public string AccentColor { get; set; } = "";

    public string SupportPhone { get; set; } = "";

    public string Domain { get; set; } = "";

    public string AndroidApplicationId { get; set; } = "";

    public string IosBundleId { get; set; } = "";

    public string? LogoSvg { get; set; }

    public ClinicStatus Status { get; set; } = ClinicStatus.Draft;

    public string? ApiKeyHash { get; set; }

    public string? ApiKeyPrefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ClinicFeature> Features { get; set; } = [];

    public List<ClinicRepo> Repos { get; set; } = [];

    public List<ProvisioningJob> Jobs { get; set; } = [];
}

public sealed class ClinicFeature
{
    public Guid ClinicId { get; set; }

    public string Key { get; set; } = "";

    public bool Enabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = "";
}

public sealed class ClinicRepo
{
    public Guid ClinicId { get; set; }

    public RepoKind Kind { get; set; }

    public string FullName { get; set; } = "";

    public string HtmlUrl { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record JobStep(string Name, StepStatus Status, string? Message, DateTimeOffset? At);

public sealed class ProvisioningJob
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public Guid Id { get; set; }

    public Guid ClinicId { get; set; }

    public bool DryRun { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    // jsonb column: [{name, status, message, at}]
    public string StepsJson { get; set; } = "[]";

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string StartedBy { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public Clinic? Clinic { get; set; }

    public List<JobStep> GetSteps() => JsonSerializer.Deserialize<List<JobStep>>(StepsJson, Json) ?? [];

    public void SetSteps(IEnumerable<JobStep> steps) => StepsJson = JsonSerializer.Serialize(steps, Json);
}

public sealed class AuditEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset At { get; set; }

    public string Actor { get; set; } = "";

    public string Action { get; set; } = "";

    public Guid? ClinicId { get; set; }

    // jsonb column
    public string DetailsJson { get; set; } = "{}";
}

using Console.Api.Domain;
using Console.Api.Features.Catalog;

namespace Console.Api.Features.Clinics;

public sealed record ClinicRequest(
    string? Slug,
    string? NameAr,
    string? NameEn,
    string? TaglineAr,
    string? TaglineEn,
    string? PrimaryColor,
    string? AccentColor,
    string? SupportPhone,
    string? Domain,
    string? AndroidApplicationId,
    string? IosBundleId,
    string? LogoSvg,
    Dictionary<string, bool>? Features);

// Fields left null stay unchanged; an empty logoSvg removes the logo. The slug is fixed once created.
public sealed record ClinicPatch(
    string? NameAr,
    string? NameEn,
    string? TaglineAr,
    string? TaglineEn,
    string? PrimaryColor,
    string? AccentColor,
    string? SupportPhone,
    string? Domain,
    string? AndroidApplicationId,
    string? IosBundleId,
    string? LogoSvg);

public sealed record RepoDto(RepoKind Kind, string FullName, string HtmlUrl);

public sealed record FeatureStateDto(string Key, bool Enabled, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public sealed record ClinicSummaryDto(
    Guid Id,
    string Slug,
    string NameAr,
    string NameEn,
    string PrimaryColor,
    string AccentColor,
    ClinicStatus Status,
    int EnabledFeatureCount,
    IReadOnlyList<RepoDto> Repos,
    DateTimeOffset CreatedAt);

public sealed record ClinicDto(
    Guid Id,
    string Slug,
    string NameAr,
    string NameEn,
    string TaglineAr,
    string TaglineEn,
    string PrimaryColor,
    string AccentColor,
    string SupportPhone,
    string Domain,
    string AndroidApplicationId,
    string IosBundleId,
    string? LogoSvg,
    ClinicStatus Status,
    bool HasApiKey,
    string? ApiKeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FeatureStateDto> Features,
    IReadOnlyList<RepoDto> Repos,
    JobDto? LatestJob);

public sealed record JobStepDto(string Name, StepStatus Status, string? Message, DateTimeOffset? At);

public sealed record JobDto(
    Guid Id,
    Guid ClinicId,
    bool DryRun,
    JobStatus Status,
    IReadOnlyList<JobStepDto> Steps,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string StartedBy,
    DateTimeOffset CreatedAt)
{
    public static JobDto From(ProvisioningJob j) =>
        new(j.Id, j.ClinicId, j.DryRun, j.Status, j.GetSteps().Select(s => new JobStepDto(s.Name, s.Status, s.Message, s.At)).ToList(), j.StartedAt, j.FinishedAt, j.StartedBy, j.CreatedAt);
}

public sealed record SetFeatureRequest(bool Enabled);

public sealed record ProvisionRequest(bool DryRun = true);

public sealed record ProvisionResponse(JobDto Job, string? ApiKey);

public sealed record RotateKeyResponse(string ApiKey, string Prefix);

public sealed record ConfigPreviewDto(System.Text.Json.JsonElement Config, IReadOnlyList<string> Repos);

public sealed record RuntimeConfigDto(Dictionary<string, bool> Features, DateTimeOffset UpdatedAt);

public static class ClinicMapping
{
    public static RepoDto ToDto(this ClinicRepo r) => new(r.Kind, r.FullName, r.HtmlUrl);

    public static ClinicSummaryDto ToSummary(this Clinic c) =>
        new(
            c.Id,
            c.Slug,
            c.NameAr,
            c.NameEn,
            c.PrimaryColor,
            c.AccentColor,
            c.Status,
            c.Features.Count(f => f.Enabled && FeatureCatalog.Find(f.Key) is { Available: true }),
            c.Repos.OrderBy(r => r.Kind).Select(ToDto).ToList(),
            c.CreatedAt);

    // One row per catalog key so the UI can render every switch, defaulting unknown rows to the catalog default.
    public static IReadOnlyList<FeatureStateDto> FeatureStates(this Clinic c) =>
        FeatureCatalog.All.Select(def =>
        {
            var row = c.Features.FirstOrDefault(f => f.Key == def.Key);
            return new FeatureStateDto(def.Key, def.Available && (row?.Enabled ?? def.DefaultEnabled), row?.UpdatedAt, row?.UpdatedBy);
        }).ToList();

    public static ClinicDto ToDto(this Clinic c, ProvisioningJob? latest) =>
        new(
            c.Id,
            c.Slug,
            c.NameAr,
            c.NameEn,
            c.TaglineAr,
            c.TaglineEn,
            c.PrimaryColor,
            c.AccentColor,
            c.SupportPhone,
            c.Domain,
            c.AndroidApplicationId,
            c.IosBundleId,
            c.LogoSvg,
            c.Status,
            c.ApiKeyHash is not null,
            c.ApiKeyPrefix,
            c.CreatedAt,
            c.UpdatedAt,
            c.FeatureStates(),
            c.Repos.OrderBy(r => r.Kind).Select(ToDto).ToList(),
            latest is null ? null : JobDto.From(latest));
}

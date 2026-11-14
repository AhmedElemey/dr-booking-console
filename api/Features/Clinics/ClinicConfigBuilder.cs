using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Console.Api.Domain;
using Console.Api.Features.Catalog;

namespace Console.Api.Features.Clinics;

// Builds the clinic.config.json that provisioning commits (schema: see the template repos' clinic.config.schema.json).
public static class ClinicConfigBuilder
{
    public const string LogoPath = "branding/logo.svg";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Build(Clinic clinic, IReadOnlyDictionary<string, bool> features, string consoleUrl, string clinicId)
    {
        var featureNode = new JsonObject();
        foreach (var def in FeatureCatalog.Available)
        {
            featureNode[def.Key] = features.TryGetValue(def.Key, out var on) ? on : def.DefaultEnabled;
        }

        var root = new JsonObject
        {
            ["$schema"] = "./clinic.config.schema.json",
            ["slug"] = clinic.Slug,
            ["name"] = new JsonObject { ["ar"] = clinic.NameAr, ["en"] = clinic.NameEn },
            ["tagline"] = new JsonObject { ["ar"] = clinic.TaglineAr, ["en"] = clinic.TaglineEn },
            ["colors"] = new JsonObject { ["primary"] = clinic.PrimaryColor, ["accent"] = clinic.AccentColor },
            ["logo"] = LogoPath,
            ["supportPhone"] = clinic.SupportPhone,
            ["website"] = new JsonObject { ["domain"] = clinic.Domain },
            ["mobile"] = new JsonObject { ["androidApplicationId"] = clinic.AndroidApplicationId, ["iosBundleId"] = clinic.IosBundleId },
            ["console"] = new JsonObject { ["url"] = consoleUrl, ["clinicId"] = clinicId },
            ["features"] = featureNode,
        };
        return root.ToJsonString(Options) + "\n";
    }

    // Repos a provisioning run creates: backend and dashboard always, mobile/website when their switch is on.
    public static IReadOnlyList<RepoKind> RequiredRepos(IReadOnlyDictionary<string, bool> features)
    {
        var kinds = new List<RepoKind> { RepoKind.Backend, RepoKind.Dashboard };
        if (features.TryGetValue("mobileApp", out var mobile) && mobile)
        {
            kinds.Add(RepoKind.Mobile);
        }

        if (features.TryGetValue("website", out var website) && website)
        {
            kinds.Add(RepoKind.Website);
        }

        return kinds;
    }

    public static string RepoName(string slug, RepoKind kind) => $"{slug}-{kind.ToString().ToLowerInvariant()}";
}

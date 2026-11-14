using System.Runtime.CompilerServices;

using Console.Api.Features.Catalog;

namespace Console.Tests;

// Every catalogue entry is available now, so the "coming soon" path (409 FEATURE_NOT_AVAILABLE) is exercised with
// a synthetic unavailable entry that exists only in the test process.
internal static class SyntheticCatalog
{
    public const string UnavailableKey = "futureFeature";

    [ModuleInitializer]
    internal static void Register() =>
        FeatureCatalog.RegisterForTesting(new FeatureDefinition(UnavailableKey, "ميزة قادمة", "Future feature", "وصف", "Description", "Growth", false, false, null));
}

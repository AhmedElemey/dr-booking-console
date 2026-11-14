namespace Console.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalog(this RouteGroupBuilder api) =>
        api.MapGet("feature-catalog", () => FeatureCatalog.All).WithTags("Catalog");
}

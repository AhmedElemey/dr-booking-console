using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Console.Api.Features.Catalog;

namespace Console.Tests;

public sealed class ClinicApiTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ConsoleApiFactory _factory;
    private HttpClient _client = null!;

    public ClinicApiTests(PostgresFixture db) => _factory = new ConsoleApiFactory(db.ConnectionString);

    public async Task InitializeAsync() => _client = await _factory.LoginAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<JsonElement> CreateAsync(string slug, object? features = null)
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic(slug, features));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.JsonAsync();
    }

    [Fact]
    public async Task Catalog_lists_available_and_coming_soon_features()
    {
        var catalog = await (await _client.GetAsync("/api/v1/feature-catalog")).JsonAsync();
        // 21 real features plus the synthetic unavailable entry registered by SyntheticCatalog.
        Assert.Equal(22, catalog.GetArrayLength());
        var available = catalog.EnumerateArray().Where(f => f.GetProperty("available").GetBoolean()).Select(f => f.GetProperty("key").GetString()).ToArray();
        Assert.Equal(
            ["mobileApp",
                "website",
                "guestBrowsing",
                "patientSelfService",
                "pushReminders",
                "smsNotifications",
                "englishLanguage",
                "multiBranch",
                "onlinePayments",
                "whatsappReminders",
                "waitingList",
                "holidayCalendar",
                "workingHoursTemplates",
                "patientRecords",
                "reviews",
                "reports",
                "promotions",
                "recallCampaigns",
                "videoConsultation",
                "familyAccounts",
                "insurance"],
            available);
        Assert.Equal([SyntheticCatalog.UnavailableKey], catalog.EnumerateArray().Where(f => !f.GetProperty("available").GetBoolean()).Select(f => f.GetProperty("key").GetString()).ToArray());
        string[] phase2 = ["onlinePayments", "whatsappReminders", "waitingList", "holidayCalendar", "workingHoursTemplates", "patientRecords", "reviews", "reports", "promotions", "recallCampaigns", "videoConsultation", "familyAccounts", "insurance"];
        Assert.All(catalog.EnumerateArray().Where(f => phase2.Contains(f.GetProperty("key").GetString())), f => Assert.False(f.GetProperty("defaultEnabled").GetBoolean()));
        var withNotes = catalog.EnumerateArray().Where(f => f.GetProperty("setupNoteEn").ValueKind == JsonValueKind.String).Select(f => f.GetProperty("key").GetString()).Order().ToArray();
        Assert.Equal(["onlinePayments", "videoConsultation", "whatsappReminders"], withNotes);
        Assert.All(catalog.EnumerateArray().Where(f => f.GetProperty("setupNoteEn").ValueKind == JsonValueKind.String), f => Assert.Equal(JsonValueKind.String, f.GetProperty("setupNoteAr").ValueKind));
        var mobile = catalog.EnumerateArray().First(f => f.GetProperty("key").GetString() == "mobileApp");
        Assert.Equal("Mobile", mobile.GetProperty("createsRepo").GetString());
        Assert.False(catalog.EnumerateArray().First(f => f.GetProperty("key").GetString() == "smsNotifications").GetProperty("defaultEnabled").GetBoolean());
        Assert.All(FeatureCatalog.All, f => Assert.False(string.IsNullOrWhiteSpace(f.NameAr) || string.IsNullOrWhiteSpace(f.DescriptionAr)));
    }

    [Fact]
    public async Task Create_makes_a_draft_with_defaults_and_requested_features()
    {
        var clinic = await CreateAsync("create-one", new { website = false, smsNotifications = true });
        Assert.Equal("Draft", clinic.GetProperty("status").GetString());
        Assert.Equal("com.createone.app", clinic.GetProperty("androidApplicationId").GetString());
        Assert.False(clinic.GetProperty("hasApiKey").GetBoolean());
        var features = clinic.GetProperty("features").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!, f => f.GetProperty("enabled").GetBoolean());
        Assert.False(features["website"]);
        Assert.True(features["smsNotifications"]);
        Assert.True(features["mobileApp"]);
        Assert.False(features["onlinePayments"]);

        var fetched = await (await _client.GetAsync($"/api/v1/clinics/{clinic.GetProperty("id").GetString()}")).JsonAsync();
        Assert.Equal("Smile Care", fetched.GetProperty("nameEn").GetString());
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("latestJob").ValueKind);
    }

    [Theory]
    [InlineData("Bad Slug", "slug")]
    [InlineData("-leading", "slug")]
    [InlineData("double--hyphen", "slug")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "slug")]
    public async Task Create_rejects_invalid_slugs(string slug, string field)
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic(slug));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Create_rejects_bad_colours_phone_names_and_unsafe_logo()
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics", new
        {
            slug = "invalid-one",
            nameAr = "",
            nameEn = "X",
            primaryColor = "red",
            accentColor = "#12345",
            supportPhone = "abc",
            domain = "https://nope",
            logoSvg = "<svg><script>alert(1)</script></svg>",
        });
        var errors = (await response.JsonAsync()).GetProperty("errors");
        foreach (var field in new[] { "nameAr", "primaryColor", "accentColor", "supportPhone", "domain", "logoSvg" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }
    }

    [Fact]
    public async Task Duplicate_slug_is_a_conflict()
    {
        await CreateAsync("dup-slug");
        var again = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic("dup-slug"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("SLUG_TAKEN", await again.CodeAsync());
    }

    [Fact]
    public async Task Coming_soon_features_cannot_be_enabled_at_creation_or_later()
    {
        var create = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic("soon-one", new { futureFeature = true }));
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        var body = await create.JsonAsync();
        Assert.Equal("FEATURE_NOT_AVAILABLE", body.GetProperty("code").GetString());
        Assert.Equal("futureFeature", body.GetProperty("feature").GetString());

        var unknown = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic("soon-two", new { teleportation = true }));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var clinic = await CreateAsync("soon-three");
        var id = clinic.GetProperty("id").GetString();
        var enable = await _client.PutAsJsonAsync($"/api/v1/clinics/{id}/features/futureFeature", new { enabled = true });
        Assert.Equal(HttpStatusCode.Conflict, enable.StatusCode);
        Assert.Equal("FEATURE_NOT_AVAILABLE", await enable.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/clinics/{id}/features/futureFeature", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/v1/clinics/{id}/features/nope", new { enabled = true })).StatusCode);
    }

    [Fact]
    public async Task Feature_switches_are_saved_audited_and_visible_in_the_runtime_pull()
    {
        var clinic = await CreateAsync("switch-one");
        var id = clinic.GetProperty("id").GetString();
        var response = await _client.PutAsJsonAsync($"/api/v1/clinics/{id}/features/guestBrowsing", new { enabled = false });
        var state = await response.JsonAsync();
        Assert.False(state.GetProperty("enabled").GetBoolean());
        Assert.Equal(ConsoleApiFactory.AdminEmail, state.GetProperty("updatedBy").GetString());

        var audit = await (await _client.GetAsync($"/api/v1/audit?clinicId={id}")).JsonAsync();
        var entry = audit.GetProperty("items").EnumerateArray().First(a => a.GetProperty("action").GetString() == "feature.changed");
        Assert.Equal("guestBrowsing", entry.GetProperty("details").GetProperty("key").GetString());
        Assert.False(entry.GetProperty("details").GetProperty("to").GetBoolean());

        var key = (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/api-key/rotate")).JsonAsync()).GetProperty("apiKey").GetString();
        var runtime = await PullAsync(id!, key);
        Assert.False(runtime.GetProperty("features").GetProperty("guestBrowsing").GetBoolean());
    }

    [Fact]
    public async Task Phase_two_features_default_off_toggle_audit_and_reach_the_runtime_pull()
    {
        string[] phase2 = ["onlinePayments", "whatsappReminders", "waitingList", "holidayCalendar", "workingHoursTemplates", "patientRecords", "reviews", "reports", "promotions", "recallCampaigns", "videoConsultation", "familyAccounts", "insurance"];
        var clinic = await CreateAsync("phase-two", new { reviews = true });
        var id = clinic.GetProperty("id").GetString()!;
        var detail = await (await _client.GetAsync($"/api/v1/clinics/{id}")).JsonAsync();
        var states = detail.GetProperty("features").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!, f => f.GetProperty("enabled").GetBoolean());
        Assert.All(phase2.Where(k => k != "reviews"), k => Assert.False(states[k]));
        Assert.True(states["reviews"]);

        foreach (var key in phase2.Where(k => k != "reviews"))
        {
            var on = await _client.PutAsJsonAsync($"/api/v1/clinics/{id}/features/{key}", new { enabled = true });
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
            Assert.True((await on.JsonAsync()).GetProperty("enabled").GetBoolean());
        }

        var audit = await (await _client.GetAsync($"/api/v1/audit?clinicId={id}&pageSize=100")).JsonAsync();
        var audited = audit.GetProperty("items").EnumerateArray().Where(a => a.GetProperty("action").GetString() == "feature.changed").Select(a => a.GetProperty("details").GetProperty("key").GetString()).ToHashSet();
        Assert.All(phase2.Where(k => k != "reviews"), k => Assert.Contains(k, audited));

        var key2 = (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/api-key/rotate")).JsonAsync()).GetProperty("apiKey").GetString();
        var features = (await PullAsync(id, key2)).GetProperty("features");
        Assert.All(phase2, k => Assert.True(features.GetProperty(k).GetBoolean()));
        Assert.False(features.TryGetProperty(SyntheticCatalog.UnavailableKey, out _));
    }

    [Fact]
    public async Task List_supports_search_status_filter_and_paging()
    {
        await CreateAsync("list-alpha");
        await CreateAsync("list-beta");
        var gamma = await CreateAsync("list-gamma");
        await _client.PostJsonAsync($"/api/v1/clinics/{gamma.GetProperty("id").GetString()}/suspend");

        var search = await (await _client.GetAsync("/api/v1/clinics?search=LIST-BE")).JsonAsync();
        Assert.Equal(1, search.GetProperty("total").GetInt32());
        Assert.Equal("list-beta", search.GetProperty("items")[0].GetProperty("slug").GetString());

        var byArabic = await (await _client.GetAsync("/api/v1/clinics?search=" + Uri.EscapeDataString("سمايل"))).JsonAsync();
        Assert.True(byArabic.GetProperty("total").GetInt32() >= 3);

        var suspended = await (await _client.GetAsync("/api/v1/clinics?status=Suspended&search=list-")).JsonAsync();
        Assert.Equal(1, suspended.GetProperty("total").GetInt32());

        var paged = await (await _client.GetAsync("/api/v1/clinics?search=list-&page=2&pageSize=2")).JsonAsync();
        Assert.Equal(3, paged.GetProperty("total").GetInt32());
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(8, paged.GetProperty("items")[0].GetProperty("enabledFeatureCount").GetInt32() + 1);
    }

    [Theory]
    [InlineData("com.my_clinic.app")]
    [InlineData("com.clinic.app_2")]
    [InlineData("my_app.clinic")]
    [InlineData("Com.Clinic.App")]
    [InlineData("single")]
    public async Task Create_rejects_an_android_application_id_that_cannot_be_a_url_scheme(string appId)
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic("app-id-bad", androidApplicationId: appId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("androidApplicationId", out var messages));
        Assert.Contains("Underscores", messages[0].GetString());
        Assert.False(errors.TryGetProperty("iosBundleId", out _));
    }

    [Fact]
    public async Task Create_accepts_a_plain_android_application_id_and_defaults_it_from_the_slug()
    {
        var explicitId = await (await _client.PostJsonAsync("/api/v1/clinics", Samples.Clinic("app-id-ok", androidApplicationId: "eg.clinic2.patient"))).JsonAsync();
        Assert.Equal("eg.clinic2.patient", explicitId.GetProperty("androidApplicationId").GetString());
        var defaulted = await CreateAsync("app-id-default");
        Assert.Equal("com.appiddefault.app", defaulted.GetProperty("androidApplicationId").GetString());
    }

    [Fact]
    public async Task Patch_rejects_an_underscore_in_the_android_application_id()
    {
        var clinic = await CreateAsync("patch-app-id");
        var id = clinic.GetProperty("id").GetString();
        var bad = await _client.PatchAsJsonAsync($"/api/v1/clinics/{id}", new { androidApplicationId = "com.my_clinic.app" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await bad.JsonAsync()).GetProperty("errors").TryGetProperty("androidApplicationId", out _));
        var good = await _client.PatchAsJsonAsync($"/api/v1/clinics/{id}", new { androidApplicationId = "com.myclinic.app" });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task Patch_updates_brand_and_identity_fields_and_validates()
    {
        var clinic = await CreateAsync("patch-one");
        var id = clinic.GetProperty("id").GetString();
        var response = await _client.PatchAsJsonAsync($"/api/v1/clinics/{id}", new { nameEn = "Smile Care Plus", primaryColor = "#112233", logoSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"4\" height=\"4\"/></svg>" });
        var updated = await response.JsonAsync();
        Assert.Equal("Smile Care Plus", updated.GetProperty("nameEn").GetString());
        Assert.Equal("#112233", updated.GetProperty("primaryColor").GetString());
        Assert.Equal("سمايل كير", updated.GetProperty("nameAr").GetString());
        Assert.Contains("<rect", updated.GetProperty("logoSvg").GetString());

        var removed = await (await _client.PatchAsJsonAsync($"/api/v1/clinics/{id}", new { logoSvg = "" })).JsonAsync();
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("logoSvg").ValueKind);

        var bad = await _client.PatchAsJsonAsync($"/api/v1/clinics/{id}", new { accentColor = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync($"/api/v1/clinics/{Guid.NewGuid()}", new { nameEn = "x" })).StatusCode);
    }

    [Fact]
    public async Task Config_preview_is_the_exact_contract_shape()
    {
        var clinic = await CreateAsync("preview-one", new { website = false });
        var id = clinic.GetProperty("id").GetString();
        var raw = await (await _client.GetAsync($"/api/v1/clinics/{id}/config-preview")).Content.ReadAsStringAsync();
        Assert.EndsWith("\n", raw);
        Assert.Contains("سمايل كير", raw);
        var config = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("./clinic.config.schema.json", config.GetProperty("$schema").GetString());
        Assert.Equal("preview-one", config.GetProperty("slug").GetString());
        Assert.Equal("Smile Care", config.GetProperty("name").GetProperty("en").GetString());
        Assert.Equal("#0F766E", config.GetProperty("colors").GetProperty("primary").GetString());
        Assert.Equal("branding/logo.svg", config.GetProperty("logo").GetString());
        Assert.Equal("smilecare.example", config.GetProperty("website").GetProperty("domain").GetString());
        Assert.Equal("com.previewone.app", config.GetProperty("mobile").GetProperty("iosBundleId").GetString());
        Assert.Equal("https://console.test", config.GetProperty("console").GetProperty("url").GetString());
        Assert.Equal(id, config.GetProperty("console").GetProperty("clinicId").GetString());
        var features = config.GetProperty("features");
        Assert.Equal(21, features.EnumerateObject().Count());
        Assert.False(features.GetProperty("website").GetBoolean());
        Assert.False(features.GetProperty("smsNotifications").GetBoolean());
    }

    [Fact]
    public async Task Preview_before_creation_returns_config_and_the_repos_to_create()
    {
        var response = await _client.PostJsonAsync("/api/v1/clinics/preview", Samples.Clinic("wizard-one", new { website = false }));
        var body = await response.JsonAsync();
        Assert.Equal(["wizard-one-backend", "wizard-one-dashboard", "wizard-one-mobile"], body.GetProperty("repos").EnumerateArray().Select(r => r.GetString()).ToArray());
        Assert.Equal("wizard-one", body.GetProperty("config").GetProperty("slug").GetString());

        var invalid = await _client.PostJsonAsync("/api/v1/clinics/preview", Samples.Clinic("Not Valid"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty(await (await _client.GetAsync("/api/v1/clinics?search=wizard-one")).JsonAsync() is { } list ? list.GetProperty("items").EnumerateArray() : []);
    }

    [Fact]
    public async Task Suspend_and_activate_change_status_and_are_audited()
    {
        var clinic = await CreateAsync("suspend-one");
        var id = clinic.GetProperty("id").GetString();
        Assert.Equal("Suspended", (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/suspend")).JsonAsync()).GetProperty("status").GetString());
        Assert.Equal("Draft", (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/activate")).JsonAsync()).GetProperty("status").GetString());
        var audit = await (await _client.GetAsync($"/api/v1/audit?clinicId={id}")).JsonAsync();
        var actions = audit.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("clinic.suspended", actions);
        Assert.Contains("clinic.activated", actions);
        Assert.Contains("clinic.created", actions);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostJsonAsync($"/api/v1/clinics/{Guid.NewGuid()}/suspend")).StatusCode);
    }

    [Fact]
    public async Task Sync_config_requires_an_active_clinic()
    {
        var clinic = await CreateAsync("sync-one");
        var response = await _client.PostJsonAsync($"/api/v1/clinics/{clinic.GetProperty("id").GetString()}/sync-config");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLINIC_NOT_ACTIVE", await response.CodeAsync());
    }

    // --- runtime pull (clinic backend -> console) ---

    private async Task<JsonElement> PullAsync(string id, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/clinics/{id}/runtime-config");
        if (key is not null)
        {
            request.Headers.Add("X-Clinic-Key", key);
        }

        var response = await _factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.JsonAsync();
    }

    private async Task<HttpStatusCode> PullStatusAsync(string id, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/clinics/{id}/runtime-config");
        if (key is not null)
        {
            request.Headers.Add("X-Clinic-Key", key);
        }

        return (await _factory.CreateClient().SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Runtime_config_needs_the_clinic_key_and_returns_every_available_feature()
    {
        var clinic = await CreateAsync("runtime-one", new { smsNotifications = true });
        var id = clinic.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, await PullStatusAsync(id, null));

        var rotated = await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/api-key/rotate")).JsonAsync();
        var key = rotated.GetProperty("apiKey").GetString()!;
        Assert.Matches("^ck_[0-9a-f]{8}_[0-9a-f]{48}$", key);
        Assert.StartsWith($"ck_{rotated.GetProperty("prefix").GetString()}_", key);

        var runtime = await PullAsync(id, key);
        var features = runtime.GetProperty("features");
        Assert.Equal(FeatureCatalog.Available.Select(f => f.Key).Order().ToArray(), features.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.True(features.GetProperty("smsNotifications").GetBoolean());
        Assert.True(runtime.TryGetProperty("updatedAt", out _));

        Assert.Equal(HttpStatusCode.Unauthorized, await PullStatusAsync(id, key + "0"));
        Assert.Equal(HttpStatusCode.Unauthorized, await PullStatusAsync(Guid.NewGuid().ToString(), key));

        // The plaintext is never stored or returned again; the clinic only exposes the prefix.
        var detail = await (await _client.GetAsync($"/api/v1/clinics/{id}")).JsonAsync();
        Assert.True(detail.GetProperty("hasApiKey").GetBoolean());
        Assert.DoesNotContain(key, detail.GetRawText());

        // Rotating invalidates the previous key.
        var newKey = (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/api-key/rotate")).JsonAsync()).GetProperty("apiKey").GetString();
        Assert.Equal(HttpStatusCode.Unauthorized, await PullStatusAsync(id, key));
        Assert.Equal(HttpStatusCode.OK, await PullStatusAsync(id, newKey));
    }

    [Fact]
    public async Task Runtime_config_turns_everything_off_for_a_suspended_clinic()
    {
        var clinic = await CreateAsync("runtime-two");
        var id = clinic.GetProperty("id").GetString()!;
        var key = (await (await _client.PostJsonAsync($"/api/v1/clinics/{id}/api-key/rotate")).JsonAsync()).GetProperty("apiKey").GetString();
        await _client.PostJsonAsync($"/api/v1/clinics/{id}/suspend");

        var features = (await PullAsync(id, key)).GetProperty("features");
        Assert.Equal(21, features.EnumerateObject().Count());
        Assert.All(features.EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));

        await _client.PostJsonAsync($"/api/v1/clinics/{id}/activate");
        Assert.True((await PullAsync(id, key)).GetProperty("features").GetProperty("mobileApp").GetBoolean());
    }
}

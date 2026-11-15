using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Console.Tests;

public sealed class ConsoleApiFactory(string connectionString, Dictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string AdminEmail = "root@example.com";
    public const string AdminPassword = "Correct-Horse-1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("SuperAdmin:Email", AdminEmail);
        builder.UseSetting("SuperAdmin:Password", AdminPassword);
        builder.UseSetting("Console:PublicUrl", "https://console.test");
        builder.UseSetting("Provisioning:Mode", "DryRun");
        foreach (var (key, value) in settings ?? [])
        {
            builder.UseSetting(key, value);
        }
    }

    public async Task<HttpClient> LoginAsync(string email = AdminEmail, string password = AdminPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ConsoleApiFactory.Json);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, ConsoleApiFactory.Json);

    public static async Task<string?> CodeAsync(this HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("code").GetString();
}

public static class Samples
{
    public static object Clinic(string slug = "smile-care", object? features = null, string? androidApplicationId = null) => new
    {
        slug,
        nameAr = "سمايل كير",
        nameEn = "Smile Care",
        taglineAr = "ابتسامتك أولا",
        taglineEn = "Your smile first",
        primaryColor = "#0F766E",
        accentColor = "#F59E0B",
        supportPhone = "+201000000000",
        domain = "smilecare.example",
        androidApplicationId,
        features,
    };
}

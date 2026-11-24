using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Console.Tests;

public sealed class AuthTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ConsoleApiFactory _factory;

    public AuthTests(PostgresFixture db) => _factory = new ConsoleApiFactory(db.ConnectionString);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Protected_endpoints_require_a_token_but_login_platform_and_health_do_not()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/clinics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/feature-catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health")).StatusCode);

        var platform = await (await anon.GetAsync("/api/v1/platform")).JsonAsync();
        Assert.Equal("Dr booking", platform.GetProperty("name").GetString());
        Assert.Equal("DryRun", platform.GetProperty("provisioningMode").GetString());
    }

    [Fact]
    public async Task Platform_name_comes_from_one_setting()
    {
        using var renamed = _factory.WithWebHostBuilder(b => b.UseSetting("Platform:Name", "Dr Booking"));
        var platform = await (await renamed.CreateClient().GetAsync("/api/v1/platform")).JsonAsync();
        Assert.Equal("Dr Booking", platform.GetProperty("name").GetString());
    }

    [Fact]
    public async Task First_admin_is_seeded_and_can_sign_in()
    {
        var client = await _factory.LoginAsync();
        var me = await (await client.GetAsync("/api/v1/me")).JsonAsync();
        Assert.Equal(ConsoleApiFactory.AdminEmail, me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_rejects_bad_credentials_with_problem_json()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = ConsoleApiFactory.AdminEmail, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("INVALID_CREDENTIALS", await response.CodeAsync());

        var missing = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        Assert.Equal("VALIDATION_FAILED", await missing.CodeAsync());
    }

    [Fact]
    public async Task Admins_can_be_created_listed_and_deactivated()
    {
        var client = await _factory.LoginAsync();
        var created = await client.PostJsonAsync("/api/v1/admins", new { email = "Second@Example.com", password = "another-long-pass", displayName = "Second" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var second = await created.JsonAsync();
        Assert.Equal("second@example.com", second.GetProperty("email").GetString());
        Assert.False(second.TryGetProperty("passwordHash", out _));

        var duplicate = await client.PostJsonAsync("/api/v1/admins", new { email = "second@example.com", password = "another-long-pass" });
        Assert.Equal("EMAIL_TAKEN", await duplicate.CodeAsync());
        var weak = await client.PostJsonAsync("/api/v1/admins", new { email = "third@example.com", password = "short" });
        Assert.Equal("VALIDATION_FAILED", await weak.CodeAsync());

        var secondClient = await _factory.LoginAsync("second@example.com", "another-long-pass");
        var list = await (await client.GetAsync("/api/v1/admins")).JsonAsync();
        Assert.True(list.GetArrayLength() >= 2);

        var id = second.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync($"/api/v1/admins/{id}/deactivate")).StatusCode);

        // The old token stops working at once and a new login is refused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondClient.GetAsync("/api/v1/me")).StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "second@example.com", password = "another-long-pass" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_themselves()
    {
        var client = await _factory.LoginAsync();
        var me = await (await client.GetAsync("/api/v1/me")).JsonAsync();
        var response = await client.PostJsonAsync($"/api/v1/admins/{me.GetProperty("id").GetString()}/deactivate");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("LAST_ADMIN", await response.CodeAsync());
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/clinics")).StatusCode);
    }
}

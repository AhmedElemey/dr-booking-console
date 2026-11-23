using System.Net;
using System.Net.Http.Json;

namespace Console.Tests;

public sealed class LoginRateLimitTests(PostgresFixture db) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Login_has_a_bounded_queue_free_limit_without_blocking_health()
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}

using System.Net;
using System.Text;

using Microsoft.AspNetCore.Http;

namespace Console.Tests;

public sealed class TrustedForwardingTests(PostgresFixture db) : IClassFixture<PostgresFixture>
{
    private static readonly Dictionary<string, string?> TrustedSettings = new()
    {
        ["ReverseProxy:KnownProxies:0"] = "192.0.2.10",
        ["ReverseProxy:KnownProxies:1"] = "192.0.2.11",
        ["ReverseProxy:ForwardLimit"] = "2",
    };

    [Theory]
    [InlineData(false, "192.0.2.10")]
    [InlineData(true, "192.0.2.99")]
    public async Task Unconfigured_or_unknown_proxies_cannot_rotate_the_login_bucket(bool configured, string sender)
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, configured ? TrustedSettings : null);
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt <= 10; attempt++)
        {
            var result = await LoginAsync(factory, sender, $"203.0.113.{attempt + 1}");
            Assert.Equal(attempt == 10 ? 429 : 400, result.Response.StatusCode);
            Assert.Equal(sender, result.Connection.RemoteIpAddress?.ToString());
        }
    }

    [Fact]
    public async Task Trusted_chain_separates_clients_but_ignores_a_spoofed_leftmost_address()
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, TrustedSettings);
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt <= 10; attempt++)
        {
            var result = await LoginAsync(factory, "192.0.2.10", $"198.51.100.{attempt + 1}, 203.0.113.1, 192.0.2.11");
            Assert.Equal(attempt == 10 ? 429 : 400, result.Response.StatusCode);
            Assert.Equal("203.0.113.1", result.Connection.RemoteIpAddress?.ToString());
        }

        var otherClient = await LoginAsync(factory, "192.0.2.10", "203.0.113.2, 192.0.2.11");
        Assert.Equal(400, otherClient.Response.StatusCode);
        Assert.Equal("203.0.113.2", otherClient.Connection.RemoteIpAddress?.ToString());
    }

    [Theory]
    [InlineData("ForwardedHeaders_Enabled", "true")]
    [InlineData("ReverseProxy:ForwardLimit", "0")]
    [InlineData("ReverseProxy:ForwardLimit", "6")]
    public async Task Unsafe_forwarding_configuration_fails_closed(string key, string value)
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, new() { [key] = value });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private static Task<HttpContext> LoginAsync(ConsoleApiFactory factory, string sender, string forwarded) =>
        factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            context.Request.Method = "POST";
            context.Request.Path = "/api/v1/auth/login";
            context.Request.ContentType = "application/json";
            var body = Encoding.UTF8.GetBytes("{\"email\":\"\",\"password\":\"\"}");
            context.Request.Body = new MemoryStream(body);
            context.Request.ContentLength = body.Length;
            context.Request.Headers["X-Forwarded-For"] = forwarded;
            context.Request.Headers["X-Forwarded-Proto"] = "https";
        });
}

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
    [InlineData("203.0.113.9, 172.70.108.104, 10.24.205.99")]
    [InlineData("1.2.3.4,203.0.113.9, 172.70.108.104, 10.24.205.99")]
    public async Task Render_chain_resolves_to_the_real_client_and_ignores_a_forged_leftmost_entry(string forwardedFor)
    {
        // Shape measured on the Render backend: sender ::1, then [client, Cloudflare, 10.x hop].
        // Cloudflare ranges are from https://www.cloudflare.com/ips-v4 and /ips-v6.
        var settings = new Dictionary<string, string?>
        {
            ["ReverseProxy:ForwardLimit"] = "3",
            ["ReverseProxy:KnownProxies:0"] = "::1",
            ["ReverseProxy:KnownNetworks:0"] = "10.0.0.0/8",
            ["ReverseProxy:KnownNetworks:1"] = "173.245.48.0/20",
            ["ReverseProxy:KnownNetworks:2"] = "103.21.244.0/22",
            ["ReverseProxy:KnownNetworks:3"] = "103.22.200.0/22",
            ["ReverseProxy:KnownNetworks:4"] = "103.31.4.0/22",
            ["ReverseProxy:KnownNetworks:5"] = "141.101.64.0/18",
            ["ReverseProxy:KnownNetworks:6"] = "108.162.192.0/18",
            ["ReverseProxy:KnownNetworks:7"] = "190.93.240.0/20",
            ["ReverseProxy:KnownNetworks:8"] = "188.114.96.0/20",
            ["ReverseProxy:KnownNetworks:9"] = "197.234.240.0/22",
            ["ReverseProxy:KnownNetworks:10"] = "198.41.128.0/17",
            ["ReverseProxy:KnownNetworks:11"] = "162.158.0.0/15",
            ["ReverseProxy:KnownNetworks:12"] = "104.16.0.0/13",
            ["ReverseProxy:KnownNetworks:13"] = "104.24.0.0/14",
            ["ReverseProxy:KnownNetworks:14"] = "172.64.0.0/13",
            ["ReverseProxy:KnownNetworks:15"] = "131.0.72.0/22",
            ["ReverseProxy:KnownNetworks:16"] = "2400:cb00::/32",
            ["ReverseProxy:KnownNetworks:17"] = "2606:4700::/32",
            ["ReverseProxy:KnownNetworks:18"] = "2803:f800::/32",
            ["ReverseProxy:KnownNetworks:19"] = "2405:b500::/32",
            ["ReverseProxy:KnownNetworks:20"] = "2405:8100::/32",
            ["ReverseProxy:KnownNetworks:21"] = "2a06:98c0::/29",
            ["ReverseProxy:KnownNetworks:22"] = "2c0f:f248::/32",
        };
        await using var factory = new ConsoleApiFactory(db.ConnectionString, settings);
        using var client = factory.CreateClient();
        var result = await LoginAsync(factory, "::1", forwardedFor);
        Assert.Equal("203.0.113.9", result.Connection.RemoteIpAddress?.ToString());
    }

    [Theory]
    [InlineData("ForwardedHeaders_Enabled", "true")]
    [InlineData("ReverseProxy:ForwardLimit", "0")]
    [InlineData("ReverseProxy:ForwardLimit", "6")]
    [InlineData("ReverseProxy:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("ReverseProxy:KnownNetworks:0", "invalid/12")]
    [InlineData("ReverseProxy:KnownNetworks:0", "10.0.0.0/33")]
    public async Task Unsafe_forwarding_configuration_fails_closed(string key, string value)
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, new() { [key] = value });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("10.24.1.9", "203.0.113.7")]
    [InlineData("192.0.2.99", "192.0.2.99")]
    public async Task Explicit_CIDR_trust_accepts_only_senders_in_the_network(string sender, string expected)
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, new()
        {
            ["ReverseProxy:KnownNetworks:0"] = "10.24.0.0/16",
            ["ReverseProxy:ForwardLimit"] = "1",
        });
        using var client = factory.CreateClient();
        var result = await LoginAsync(factory, sender, "198.51.100.8, 203.0.113.7");
        Assert.Equal(expected, result.Connection.RemoteIpAddress?.ToString());
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

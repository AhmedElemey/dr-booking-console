using System.Net;

using Microsoft.AspNetCore.HttpOverrides;

namespace Console.Api.Infrastructure;

public static class TrustedForwarding
{
    public static void AddTrustedForwarding(this IServiceCollection services, IConfiguration config)
    {
        // Never use ASPNETCORE_FORWARDEDHEADERS_ENABLED: it trusts every sender.
        if (config.GetValue<bool>("ForwardedHeaders_Enabled"))
        {
            throw new InvalidOperationException("Use explicit ReverseProxy:KnownProxies, not unrestricted forwarded headers.");
        }

        var proxies = config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        var addresses = proxies.Select(IPAddress.Parse).ToArray();
        var limit = config.GetValue("ReverseProxy:ForwardLimit", 1);
        if (limit is < 1 or > 5)
        {
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be between 1 and 5.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardLimit = limit;
            options.ForwardedHeaders = addresses.Length == 0 ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // No implicit loopback or all-private-network trust. With no explicit
            // proxies configured, forwarding is off, not open to every sender.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var address in addresses)
            {
                options.KnownProxies.Add(address);
            }
        });
    }
}

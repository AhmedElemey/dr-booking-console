# Production deployment

## Forwarding behind Render (required for every clinic deployment)

Required step for every clinic deployment of this service on Render. Without it, `X-Forwarded-For` is not trusted and every rate limit (OTP per IP, login) keys on the proxy address instead of the real client. In Production the service logs one warning at startup when neither `ReverseProxy__KnownProxies` nor `ReverseProxy__KnownNetworks` is set.

Measured on Render (backend-test): the app's direct sender is loopback (`::1`). `X-Forwarded-For` arrives as `[client-supplied entries, real client, Cloudflare address, 10.x Render hop]`, and `CF-Connecting-IP` is the real client. A forged leftmost entry stays leftmost. The 10.x hop varied (10.24, 10.27, 10.28). Limit 1 or 2 returns the hop address, not the client, so use limit 3. Nothing is trusted by default in code. Set these environment variables on the Render service:

```
ReverseProxy__KnownProxies__0=::1
ReverseProxy__ForwardLimit=3
ReverseProxy__KnownNetworks__0=10.0.0.0/8
ReverseProxy__KnownNetworks__1=173.245.48.0/20
ReverseProxy__KnownNetworks__2=103.21.244.0/22
ReverseProxy__KnownNetworks__3=103.22.200.0/22
ReverseProxy__KnownNetworks__4=103.31.4.0/22
ReverseProxy__KnownNetworks__5=141.101.64.0/18
ReverseProxy__KnownNetworks__6=108.162.192.0/18
ReverseProxy__KnownNetworks__7=190.93.240.0/20
ReverseProxy__KnownNetworks__8=188.114.96.0/20
ReverseProxy__KnownNetworks__9=197.234.240.0/22
ReverseProxy__KnownNetworks__10=198.41.128.0/17
ReverseProxy__KnownNetworks__11=162.158.0.0/15
ReverseProxy__KnownNetworks__12=104.16.0.0/13
ReverseProxy__KnownNetworks__13=104.24.0.0/14
ReverseProxy__KnownNetworks__14=172.64.0.0/13
ReverseProxy__KnownNetworks__15=131.0.72.0/22
ReverseProxy__KnownNetworks__16=2400:cb00::/32
ReverseProxy__KnownNetworks__17=2606:4700::/32
ReverseProxy__KnownNetworks__18=2803:f800::/32
ReverseProxy__KnownNetworks__19=2405:b500::/32
ReverseProxy__KnownNetworks__20=2405:8100::/32
ReverseProxy__KnownNetworks__21=2a06:98c0::/29
ReverseProxy__KnownNetworks__22=2c0f:f248::/32
```

Cloudflare ranges come from https://www.cloudflare.com/ips-v4 and https://www.cloudflare.com/ips-v6. Re-check them every few months (https://www.cloudflare.com/ips/) and update `ReverseProxy__KnownNetworks__N`. `ASPNETCORE_FORWARDEDHEADERS_ENABLED` must stay unset.

After deploy, confirm two different clients get separate rate-limit buckets. `TrustedForwardingTests` covers `[client, Cloudflare, 10.x]` and `[1.2.3.4, client, Cloudflare, 10.x]` from `::1`; both resolve to the client.

# Connected test hosting

The Dockerfile builds Angular and the .NET8 API into one service, reducing dynamic services and keeping the browser/API same-origin. Use only the Free compute choice and a separate Neon Postgres16 database. Never choose a paid default or add a card.

Set private environment values in the host, never the repo:
- `ConnectionStrings__Default`: TLS-enabled Npgsql string for a new isolated test database
- `Jwt__Key`: generated separate signing secret, at least32 characters
- `SuperAdmin__Email`: owner-approved test administrator email
- `SuperAdmin__Password`: private generated password, never README development credentials
- `ASPNETCORE_ENVIRONMENT=Production`
- `Provisioning__Mode=DryRun`
- `Console__PublicUrl`: actual HTTPS console URL
- `Cors__Origins`: exact console HTTPS origin

Leave GitHub__Token absent. Do not run real provisioning. Never invite users or upload real clinic/patient data. New first admin is seeded at startup only if none exists. Database migrations run at startup by default. The API should remain authenticated; `/health` and the UI login page are anonymous, unknown `/api/*` remains404.

Required validation after deploy: inspect final $0/month, health200, UI/login/deep links load, missing/incorrect admin credentials fail, real login works with secure credentials, unknown API404, protected API401, and dry-run provisioning does not create repositories. Docker build is not locally verified because Docker is unavailable; .NET and Angular build/test checks are separate.


## Forwarding and startup guards (review update)

`ReverseProxy__KnownProxies__0` accepts a verified proxy IP; `ReverseProxy__KnownNetworks__0` accepts a verified CIDR. No trust is enabled by default. Do not copy a broad private range from an example. Confirm Render's actual ingress topology and the narrowest supported trusted range before enabling forwarding. Render outbound ranges are not ingress proxy ranges. Keep `ReverseProxy__ForwardLimit=1` unless the verified topology needs more hops (maximum 5). Global CIDRs are refused. `ASPNETCORE_FORWARDEDHEADERS_ENABLED` must remain unset.

After deployment, temporarily inspect the resolved client IP with synthetic requests from two clients and a forged leftmost forwarded entry, then remove diagnostic logging. Do not log OTPs, tokens or patient data. Production/deployment remains blocked until this check proves distinct legitimate clients and rejects spoofed addresses. No broad CIDR is preconfigured by this change.

Outside Development, JWT keys containing the public development markers are refused. Use a private generated signing key, not a value copied from development settings.

Outside Development, the published default seeded administrator password and configured passwords shorter than 12 characters are refused at startup, even if migration is disabled. An omitted seed password remains allowed for an existing administrator database. Docker is not installed in the current validation environment; a real image build and container startup test remain required before deployment. Set the host PORT to 8080 to match Kestrel.

### Forwarding on Render (same topology as the backend)

The backend's measured Render chain is `[client-supplied entries, real client, Cloudflare address, 10.x hop]` from sender `::1` (see the backend DEPLOYMENT-TEST.md). Nothing is trusted by default in code; set these environment variables on the console service (Cloudflare ranges from https://www.cloudflare.com/ips-v4 and https://www.cloudflare.com/ips-v6):

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

`ASPNETCORE_FORWARDEDHEADERS_ENABLED` stays unset. `TrustedForwardingTests` covers `[client, Cloudflare, 10.x]` and `[1.2.3.4, client, Cloudflare, 10.x]` from `::1`; both resolve to the client. After deploy, confirm the login rate-limit bucket separates two real clients, since the console itself was not measured with a log.

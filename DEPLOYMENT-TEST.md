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

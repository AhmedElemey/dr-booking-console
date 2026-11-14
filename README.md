# Super admin console

The control plane for a multi-clinic platform. A super admin creates a clinic (identity, brand, features), the console provisions its GitHub repositories from template repos, and each clinic's backend pulls its feature switches from the console at runtime.

The platform display name is one setting, `Platform__Name` (default `Moonlight`), used by the API and the web UI. Change it there only.

```
api/         .NET 8 Web API (Console.Api): auth, clinics, feature catalog, provisioning engine, audit
api.tests/   xUnit unit and integration tests (Postgres)
web/         Angular 21 app (Material, ar/en with RTL, English default)
docker-compose.yml   local Postgres
```

## Run locally

Prerequisites: .NET 8 SDK, Node 22, Docker (or any Postgres 16).

```bash
docker compose up -d                      # Postgres on :5432, database "console"
cd api && dotnet run                      # http://localhost:5280 (Development settings)
cd web && npm ci && npm start             # http://localhost:4200
```

In Development the API migrates the database on startup and seeds the first super admin from `appsettings.Development.json` (`admin@example.com` / `ChangeMe-12345`). Swagger is at `/swagger`.

### Demo without the API (mock mode)

```bash
cd web && npm run start:mock              # http://localhost:4300, any email and password
```

An in-memory fake API serves three sample clinics and simulates provisioning jobs, so the UI can be demoed and screenshotted on its own. The mock code is swapped in only by the `mock` build configuration and is not part of the production bundle.

## Configuration (environment variables)

| Variable | Meaning | Default |
|---|---|---|
| `ConnectionStrings__Default` | Postgres connection string | required |
| `Jwt__Key` | HMAC signing key for access tokens (at least 32 characters) | required |
| `Jwt__ExpiresHours` | access token lifetime | `8` |
| `SuperAdmin__Email`, `SuperAdmin__Password` | first super admin, seeded on startup only when no admin exists | none |
| `Platform__Name` | platform display name shown everywhere | `Moonlight` |
| `Console__PublicUrl` | public origin of this console; written into every clinic's `clinic.config.json` and the `CONSOLE_URL` Actions variable | empty |
| `Cors__Origins` | comma separated allowed web origins | `http://localhost:4200` |
| `Database__AutoMigrate` | apply migrations on startup | `true` |
| `Provisioning__Mode` | `DryRun` or `GitHub` (see below) | `DryRun` |
| `GitHub__Token` | fine-grained personal access token (never logged) | none |
| `GitHub__Owner` | user or organisation that receives the clinic repositories | none |
| `GitHub__TemplateOwner` | owner of the template repositories | same as `GitHub__Owner` |
| `GitHub__Templates__Backend` / `Dashboard` / `Mobile` / `Website` | template repository names | `dr-booking-backend`, `dr-booking-dashboard`, `dr-booking-mobile`, `dr-booking-website` |
| `GitHub__PollIntervalMs`, `GitHub__ReadyTimeoutSeconds` | wait for a new repository's `clinic.config.json` to appear | `3000`, `120` |

The web app reads the API origin from `web/src/environments/environment*.ts` (production builds use the same origin, so serve the web files and proxy `/api` to the API).

## Dry run vs real mode

- `Provisioning__Mode=DryRun` (default): every job is a dry run. Each step is recorded ("Would create private repository ...") but nothing touches GitHub, no API key is generated and the clinic status is unchanged. The UI shows a "Dry-run mode" badge.
- `Provisioning__Mode=GitHub`: a job runs for real unless it was started with the dry-run box ticked (the wizard and the provision dialog default to ticked). A dry-run job never touches GitHub, even in GitHub mode.

A real run, for each repository (backend and dashboard always, mobile and website when their feature is on):

1. creates `{slug}-{kind}` from the template (`POST /repos/{templateOwner}/{template}/generate`, private) and waits up to 2 minutes for `clinic.config.json` to appear,
2. commits `clinic.config.json` (message `Brand for {name}`) and `branding/logo.svg` when a logo was uploaded,
3. sets the Actions variables `CONSOLE_URL` and `CLINIC_ID`,

then records the repositories and marks the clinic Active. It is idempotent: a repository that already exists is reused, files that already hold the same content are skipped, and variables are updated in place, so after a failure you can retry and finished steps are skipped. On failure the job and the clinic are marked Failed with the GitHub error message and nothing is deleted.

### Creating the GitHub token

Create a fine-grained personal access token (Settings, Developer settings, Personal access tokens, Fine-grained tokens):

- Resource owner: the owner in `GitHub__Owner`.
- Repository access: all repositories (new clinic repositories do not exist yet), and make sure the template repositories are readable by the token.
- Permissions: **Administration** read and write, **Contents** read and write, **Variables** read and write, **Metadata** read.

Set it as `GitHub__Token`. Mark each template repository as a template repository in its settings. The template repositories must contain `clinic.config.json` at their root.

## Clinic API key and runtime features

The first real provisioning run generates a clinic API key, shown **once** (`ck_<prefix>_<secret>`). Only its SHA-256 hash and prefix are stored. "Rotate API key" issues a new one. Set these on the clinic's backend:

```
Console__Url=<this console's public URL>
Console__ClinicId=<clinic id>
Console__ApiKey=<the key>
```

The backend then calls `GET /api/v1/clinics/{clinicId}/runtime-config` with header `X-Clinic-Key` and receives `{ "features": { ... }, "updatedAt": "..." }` for every available feature (all false while the clinic is suspended). Feature switches saved in the console take effect on the backend's next pull.

## Tests and checks

```bash
# api (needs Postgres; set TEST_DATABASE_URL, otherwise Testcontainers starts one)
export TEST_DATABASE_URL="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test

# web
cd web && npm run lint && npm test && npm run build && npm run build:mock
```

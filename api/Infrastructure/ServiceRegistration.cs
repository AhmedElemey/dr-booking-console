using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Features.Audit;
using Console.Api.Features.Auth;
using Console.Api.Features.Catalog;
using Console.Api.Features.Clinics;
using Console.Api.Features.Platform;
using Console.Api.Features.Provisioning;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Console.Api.Infrastructure;

public static class ServiceRegistration
{
    public static void AddConsole(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var services = builder.Services;

        services.AddTrustedForwarding(config);

        services.Configure<PlatformOptions>(config.GetSection(PlatformOptions.Section));
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<SuperAdminSeedOptions>(config.GetSection(SuperAdminSeedOptions.Section));
        services.Configure<ConsoleOptions>(config.GetSection(ConsoleOptions.Section));
        services.Configure<ProvisioningOptions>(config.GetSection(ProvisioningOptions.Section));
        services.Configure<GitHubOptions>(config.GetSection(GitHubOptions.Section));

        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be set (environment variable Jwt__Key) and at least 32 characters long.");
        }

        if (!builder.Environment.IsDevelopment())
        {
            if (jwt.Key.Contains("development-only", StringComparison.OrdinalIgnoreCase) ||
                jwt.Key.Contains("change-me", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Jwt:Key must not use a development signing key outside Development.");
            }

            var password = config["SuperAdmin:Password"];
            if (password is not null && (password.Length < 12 || password == "ChangeMe-12345"))
            {
                throw new InvalidOperationException("SuperAdmin:Password must be private, non-default and at least 12 characters outside Development.");
            }
        }

        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ConsoleDbContext>(options => options
            .UseNpgsql(config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is not set."))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<AuthService>();
        services.AddScoped<AuditLog>();
        services.AddScoped<ProvisioningEngine>();
        services.AddSingleton<ProvisioningQueue>();
        services.AddHostedService<ProvisioningWorker>();
        services.AddSingleton<IProvisionerFactory, ProvisionerFactory>();
        services.AddTransient<DryRunProvisioner>();
        services.AddHttpClient<GitHubRestProvisioner>((sp, client) => GitHubRestProvisioner.Configure(client, sp.GetRequiredService<IOptions<GitHubOptions>>().Value));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Issuer,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromMinutes(1),
            };
            options.Events = new JwtBearerEvents
            {
                // A deactivated super admin loses access immediately, not when the token expires.
                OnTokenValidated = async context =>
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<ConsoleDbContext>();
                    var id = context.Principal!.Id();
                    if (!await db.Admins.AnyAsync(a => a.Id == id && a.IsActive))
                    {
                        context.Fail("inactive");
                    }
                },
            };
        });
        services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var origins = (config["Cors:Origins"] ?? "http://localhost:4200").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "RATE_LIMITED",
                    status = StatusCodes.Status429TooManyRequests,
                    title = "Too many sign-in attempts. Try again in a minute.",
                }, cancellationToken);
            };
            options.AddPolicy("console-login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "Clinic console API", Version = "v1" });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
            });
        });
    }

    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPlatform();
        api.MapAuth();
        api.MapCatalog();
        api.MapClinics();
        api.MapAudit();
    }
}

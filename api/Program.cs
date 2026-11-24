using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Features.Auth;
using Console.Api.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddConsole();

var app = builder.Build();
app.WarnIfForwardingUntrusted();

app.UseForwardedHeaders();
// The admin console must never be indexed, including static assets and error responses.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
    await next();
});
app.UseExceptionHandler();
app.UseStatusCodePages(context => Problems.WriteAsync(context.HttpContext, ErrorCodes.FromStatus(context.HttpContext.Response.StatusCode)));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{app.Services.GetRequiredService<IOptions<PlatformOptions>>().Value.Name} console API v1"));
}

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ConsoleDbContext>();
    await db.Database.MigrateAsync();
    await AdminSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IOptions<SuperAdminSeedOptions>>(), scope.ServiceProvider.GetRequiredService<TimeProvider>(), app.Logger);
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /\n", "text/plain")).AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapApi();
// Only non-API browser navigation falls back to the Angular entry point.
// Unknown API routes must remain 404, never a successful HTML response.
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;

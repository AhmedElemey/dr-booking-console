using Console.Api.Common;
using Console.Api.Data;
using Console.Api.Features.Auth;
using Console.Api.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddConsole();

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapApi();

app.Run();

public partial class Program;

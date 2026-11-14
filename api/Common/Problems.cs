using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;

namespace Console.Api.Common;

// RFC 7807 application/problem+json with a stable machine `code`.
public static class Problems
{
    public static async Task WriteAsync(
        HttpContext context,
        string code,
        string? detail = null,
        IDictionary<string, string[]>? errors = null,
        IDictionary<string, object?>? extensions = null)
    {
        var status = ErrorCodes.StatusOf(code);
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://console.invalid/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            ["title"] = ErrorCodes.TitleOf(code),
            ["status"] = status,
            ["code"] = code,
            ["detail"] = detail ?? ErrorCodes.DetailOf(code),
            ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
        };
        if (errors is { Count: > 0 })
        {
            body["errors"] = errors;
        }

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                body[key] = value;
            }
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(body, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ApiException api)
        {
            await Problems.WriteAsync(httpContext, api.Code, api.Detail, api.Errors, api.Extensions);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        await Problems.WriteAsync(httpContext, ErrorCodes.InternalError);
        return true;
    }
}

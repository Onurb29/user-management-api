using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace UserManagementAPI.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        var path = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/unmatched";
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        try { await next(context); }
        catch { failed = true; throw; }
        finally
        {
            // Route templates avoid logging arbitrary IDs, email-like path values,
            // query strings, bodies, authentication headers or caller-supplied correlation IDs.
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms; correlation {CorrelationId}",
                context.Request.Method, path, failed ? 500 : context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
        }
    }
}

using Microsoft.AspNetCore.Diagnostics;

namespace UserManagementAPI.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Log type and correlation only: exception messages can contain secrets or SQL data.
        logger.LogError("Request failed with {ExceptionType}; correlation {CorrelationId}",
            exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}

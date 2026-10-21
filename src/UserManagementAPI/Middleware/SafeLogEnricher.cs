using Serilog.Core;
using Serilog.Events;

namespace UserManagementAPI.Middleware;

// ASP.NET logging scopes can add raw RequestPath even when our message uses a
// route template. Strip ambient properties before any sink receives the event.
public sealed class SafeLogEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> AllowedProperties =
        ["SourceContext", "Method", "Path", "StatusCode", "ElapsedMs", "CorrelationId", "ExceptionType"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in logEvent.Properties.Keys.Where(name => !AllowedProperties.Contains(name)).ToArray())
            logEvent.RemovePropertyIfPresent(name);
    }
}

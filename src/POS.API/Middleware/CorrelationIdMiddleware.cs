using System.Diagnostics;

namespace POS.API.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ContextKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        context.Items[ContextKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            [ContextKey] = correlationId,
        });

        await _next(context);
    }
}

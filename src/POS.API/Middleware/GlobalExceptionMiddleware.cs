using System.Text.Json;
using POS.Application.Exceptions;

namespace POS.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (AppValidationException ex)
        {
            await HandleExceptionAsync(context, StatusCodes.Status400BadRequest, "ValidationError", ex.Message, ex);
        }
        catch (UnauthorizedAccessException)
        {
            await HandleExceptionAsync(context, StatusCodes.Status403Forbidden, "Forbidden", "Access denied.", null);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, StatusCodes.Status500InternalServerError, "UnhandledError", "An unexpected error occurred.", ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, int statusCode, string code, string message, Exception? exception)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ContextKey, out var value)
            ? value?.ToString()
            : null;

        if (exception is null)
        {
            _logger.LogWarning(
                "Request failed. StatusCode={StatusCode} Code={Code} Path={Path} CorrelationId={CorrelationId}",
                statusCode,
                code,
                context.Request.Path,
                correlationId);
        }
        else
        {
            _logger.LogError(
                exception,
                "Request failed. StatusCode={StatusCode} Code={Code} Path={Path} CorrelationId={CorrelationId}",
                statusCode,
                code,
                context.Request.Path,
                correlationId);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            error = code,
            message,
            correlationId,
            traceId = context.TraceIdentifier,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

namespace POS.Application.Models;

public sealed class AuditLogEntry
{
    public long? UserId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string ResourceType { get; init; } = string.Empty;

    public string? ResourceId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }

    public string? MetadataJson { get; init; }

    public string? ErrorMessage { get; init; }
}

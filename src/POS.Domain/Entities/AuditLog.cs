using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class AuditLog : EntityBase
{
    public long? UserId { get; set; }

    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string? MetadataJson { get; set; }

    public string? ErrorMessage { get; set; }
}

using POS.Application.Abstractions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly PosDbContext _dbContext;

    public AuditLogService(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            UserId = entry.UserId,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Status = entry.Status,
            CorrelationId = entry.CorrelationId,
            MetadataJson = entry.MetadataJson,
            ErrorMessage = entry.ErrorMessage,
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

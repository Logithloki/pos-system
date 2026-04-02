using POS.Application.Models;

namespace POS.Application.Abstractions;

public interface IAuditLogService
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

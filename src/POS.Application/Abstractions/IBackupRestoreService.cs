using POS.Application.Models;

namespace POS.Application.Abstractions;

public interface IBackupRestoreService
{
    Task<BackupResult> CreateBackupAsync(bool isAutomatic, CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreBackupAsync(RestoreRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BackupResult>> ListBackupsAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Infrastructure.Backup;

namespace POS.Infrastructure.Services;

public sealed class AutomaticBackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackupOptions> _backupOptions;
    private readonly ILogger<AutomaticBackupHostedService> _logger;

    public AutomaticBackupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BackupOptions> backupOptions,
        ILogger<AutomaticBackupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _backupOptions = backupOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, _backupOptions.Value.AutomaticBackupIntervalMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();
                await backupService.CreateBackupAsync(isAutomatic: true, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic backup failed.");
            }
        }
    }
}

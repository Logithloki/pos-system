using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Infrastructure.Backup;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public sealed class BackupRestoreService : IBackupRestoreService
{
    private const int MaxListedBackups = 200;
    private const string RequiredSchemaTable = "SalesOrders";

    private readonly PosDbContext _dbContext;
    private readonly IOptions<BackupOptions> _backupOptions;
    private readonly ISystemClock _clock;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<BackupRestoreService> _logger;

    public BackupRestoreService(
        PosDbContext dbContext,
        IOptions<BackupOptions> backupOptions,
        ISystemClock clock,
        IAuditLogService auditLogService,
        ILogger<BackupRestoreService> logger)
    {
        _dbContext = dbContext;
        _backupOptions = backupOptions;
        _clock = clock;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<BackupResult> CreateBackupAsync(bool isAutomatic, CancellationToken cancellationToken = default)
    {
        var resolved = ResolveOptions();

        if (!File.Exists(resolved.DatabaseFilePath))
        {
            throw new AppValidationException($"Database file was not found: {resolved.DatabaseFilePath}");
        }

        Directory.CreateDirectory(resolved.BackupDirectory);

        var backupType = isAutomatic ? "auto" : "manual";
        var backupFileName = $"pos_backup_{_clock.UtcNow:yyyyMMddHHmmssfff}_{backupType}.db";
        var backupFilePath = Path.Combine(resolved.BackupDirectory, backupFileName);

        await _dbContext.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        File.Copy(resolved.DatabaseFilePath, backupFilePath, overwrite: false);
        var checksum = ComputeFileChecksum(backupFilePath);

        _dbContext.BackupRecords.Add(
            new BackupRecord
            {
                FilePath = backupFilePath,
                ChecksumSha256 = checksum,
                IsAutomatic = isAutomatic,
                IsRestoreOperation = false,
                Status = "Completed",
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await PurgeExpiredBackupsAsync(resolved, cancellationToken);

        await _auditLogService.WriteAsync(
            new AuditLogEntry
            {
                Action = isAutomatic ? "AutomaticBackup" : "ManualBackup",
                ResourceType = "Backup",
                ResourceId = backupFilePath,
                Status = "Success",
                MetadataJson = JsonSerializer.Serialize(new { checksum }),
            },
            cancellationToken);

        _logger.LogInformation("Backup created at {BackupFilePath}.", backupFilePath);

        return new BackupResult
        {
            FilePath = backupFilePath,
            ChecksumSha256 = checksum,
            CreatedUtc = _clock.UtcNow,
            IsAutomatic = isAutomatic,
        };
    }

    public async Task<RestoreResult> RestoreBackupAsync(RestoreRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BackupFilePath))
        {
            throw new AppValidationException("Backup file path is required.");
        }

        var resolved = ResolveOptions();
        var requestedBackupPath = request.BackupFilePath.Trim();
        var backupFilePath = Path.GetFullPath(requestedBackupPath);
        var backupDirectoryPath = Path.GetFullPath(resolved.BackupDirectory);
        var pathBoundary = backupDirectoryPath.EndsWith(Path.DirectorySeparatorChar)
            ? backupDirectoryPath
            : backupDirectoryPath + Path.DirectorySeparatorChar;

        if (!backupFilePath.StartsWith(pathBoundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppValidationException("Backup file must be inside the configured backup directory.");
        }

        if (!File.Exists(backupFilePath))
        {
            throw new AppValidationException($"Backup file not found: {backupFilePath}");
        }

        await ValidateBackupFileAsync(backupFilePath, cancellationToken);

        string? safetyBackupPath = null;
        if (request.CreateSafetyBackupBeforeRestore)
        {
            var safetyBackup = await CreateBackupAsync(isAutomatic: false, cancellationToken);
            safetyBackupPath = safetyBackup.FilePath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(resolved.DatabaseFilePath)!);

        await _dbContext.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        File.Copy(backupFilePath, resolved.DatabaseFilePath, overwrite: true);

        var checksum = ComputeFileChecksum(backupFilePath);
        _dbContext.BackupRecords.Add(
            new BackupRecord
            {
                FilePath = backupFilePath,
                ChecksumSha256 = checksum,
                IsAutomatic = false,
                IsRestoreOperation = true,
                Status = "Restored",
                Note = "Restore operation completed.",
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            new AuditLogEntry
            {
                Action = "DatabaseRestore",
                ResourceType = "Backup",
                ResourceId = backupFilePath,
                Status = "Success",
                MetadataJson = JsonSerializer.Serialize(new { safetyBackupPath }),
            },
            cancellationToken);

        _logger.LogInformation("Database restore completed from {BackupFilePath}.", backupFilePath);

        return new RestoreResult
        {
            IsSuccessful = true,
            Message = "Restore completed.",
            SafetyBackupPath = safetyBackupPath,
        };
    }

    public Task<IReadOnlyCollection<BackupResult>> ListAllBackupsAsync(CancellationToken cancellationToken = default)
    {
        var resolved = ResolveOptions();

        if (!Directory.Exists(resolved.BackupDirectory))
        {
            return Task.FromResult<IReadOnlyCollection<BackupResult>>(Array.Empty<BackupResult>());
        }

        var files = Directory.GetFiles(resolved.BackupDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path)
            .Select(
                path => new BackupResult
                {
                    FilePath = path,
                    ChecksumSha256 = TryComputeFileChecksum(path),
                    CreatedUtc = File.GetCreationTimeUtc(path),
                    IsAutomatic = path.Contains("_auto", StringComparison.OrdinalIgnoreCase),
                })
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<BackupResult>>(files);
    }

    public Task<IReadOnlyCollection<BackupResult>> ListBackupsAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new AppValidationException("Skip must be zero or greater.");
        }

        if (take <= 0)
        {
            throw new AppValidationException("Take must be greater than zero.");
        }

        var normalizedTake = Math.Min(take, MaxListedBackups);
        var resolved = ResolveOptions();

        if (!Directory.Exists(resolved.BackupDirectory))
        {
            return Task.FromResult<IReadOnlyCollection<BackupResult>>(Array.Empty<BackupResult>());
        }

        var files = Directory.GetFiles(resolved.BackupDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => path)
            .Skip(skip)
            .Take(normalizedTake)
            .Select(
                path => new BackupResult
                {
                    FilePath = path,
                    ChecksumSha256 = TryComputeFileChecksum(path),
                    CreatedUtc = File.GetCreationTimeUtc(path),
                    IsAutomatic = path.Contains("_auto", StringComparison.OrdinalIgnoreCase),
                })
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<BackupResult>>(files);
    }

    private async Task PurgeExpiredBackupsAsync(ResolvedBackupOptions options, CancellationToken cancellationToken)
    {
        if (options.RetentionDays <= 0 || !Directory.Exists(options.BackupDirectory))
        {
            return;
        }

        var threshold = _clock.UtcNow.AddDays(-options.RetentionDays);
        var expiredFiles = Directory.GetFiles(options.BackupDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .Where(path => File.GetCreationTimeUtc(path) < threshold)
            .ToArray();

        foreach (var file in expiredFiles)
        {
            File.Delete(file);
        }

        var expiredRecords = await _dbContext.BackupRecords
            .Where(x => x.CreatedUtc < threshold && !x.IsRestoreOperation)
            .ToListAsync(cancellationToken);

        if (expiredRecords.Count > 0)
        {
            _dbContext.BackupRecords.RemoveRange(expiredRecords);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private ResolvedBackupOptions ResolveOptions()
    {
        var options = _backupOptions.Value;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var dbPath = options.DatabaseFilePath;
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine(localAppData, "POS", "data", "pos.db");
        }
        else if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.Combine(localAppData, "POS", dbPath);
        }

        var backupDirectory = options.BackupDirectory;
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            backupDirectory = Path.Combine(localAppData, "POS", "backups");
        }
        else if (!Path.IsPathRooted(backupDirectory))
        {
            backupDirectory = Path.Combine(localAppData, "POS", backupDirectory);
        }

        return new ResolvedBackupOptions(dbPath, backupDirectory, Math.Max(1, options.RetentionDays));
    }

    private static string ComputeFileChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private string TryComputeFileChecksum(string filePath)
    {
        try
        {
            return ComputeFileChecksum(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to compute checksum for backup file {BackupFilePath}.", filePath);
            return string.Empty;
        }
    }

    private static async Task ValidateBackupFileAsync(string backupFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = backupFilePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using (var integrityCommand = connection.CreateCommand())
            {
                integrityCommand.CommandText = "PRAGMA integrity_check;";
                var integrityResult = await integrityCommand.ExecuteScalarAsync(cancellationToken);
                if (!string.Equals(integrityResult?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AppValidationException("Backup file failed SQLite integrity check.");
                }
            }

            await using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
            schemaCommand.Parameters.AddWithValue("$tableName", RequiredSchemaTable);

            var schemaResult = await schemaCommand.ExecuteScalarAsync(cancellationToken);
            if (schemaResult is null)
            {
                throw new AppValidationException("Backup file schema is not compatible with this POS database.");
            }
        }
        catch (AppValidationException)
        {
            throw;
        }
        catch
        {
            throw new AppValidationException("Backup file is invalid or unreadable.");
        }
    }

    private sealed record ResolvedBackupOptions(string DatabaseFilePath, string BackupDirectory, int RetentionDays);
}

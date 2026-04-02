using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Backup;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services;

namespace POS.Tests.Phase1;

public sealed class BackupRestoreTests
{
    [Fact]
    public async Task CreateBackup_Should_Create_File_And_Record_Metadata()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "POS.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var dbPath = Path.Combine(tempRoot, "pos.db");
        var backupPath = Path.Combine(tempRoot, "backups");

        PosDbContext? context = null;

        try
        {
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            context = new PosDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var service = new BackupRestoreService(
                context,
                Options.Create(new BackupOptions
                {
                    DatabaseFilePath = dbPath,
                    BackupDirectory = backupPath,
                    RetentionDays = 7,
                    AutomaticBackupIntervalMinutes = 60,
                }),
                new TestClock(),
                new AuditLogService(context),
                NullLogger<BackupRestoreService>.Instance);

            var result = await service.CreateBackupAsync(isAutomatic: false);

            Assert.True(File.Exists(result.FilePath));
            Assert.False(string.IsNullOrWhiteSpace(result.ChecksumSha256));
            Assert.StartsWith(backupPath, result.FilePath, StringComparison.OrdinalIgnoreCase);

            var recordCount = await context.BackupRecords.CountAsync();
            Assert.Equal(1, recordCount);
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class TestClock : POS.Application.Abstractions.ISystemClock
    {
        public DateTime UtcNow => new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    }
}

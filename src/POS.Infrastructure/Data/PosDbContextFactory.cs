using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace POS.Infrastructure.Data;

public sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var databaseFilePath = Environment.GetEnvironmentVariable("POS_DB_PATH");

        if (string.IsNullOrWhiteSpace(databaseFilePath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            databaseFilePath = Path.Combine(localAppData, "POS", "data", "pos.db");
        }

        var directory = Path.GetDirectoryName(databaseFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite($"Data Source={databaseFilePath}");

        return new PosDbContext(optionsBuilder.Options);
    }
}

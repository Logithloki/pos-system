using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Infrastructure.Backup;
using POS.Infrastructure.Data;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Security;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPosInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<ReceiptPrintingOptions>(configuration.GetSection(ReceiptPrintingOptions.SectionName));

        services.PostConfigure<BackupOptions>(options =>
        {
            options.DatabaseFilePath = ResolveLocalPath(options.DatabaseFilePath, Path.Combine("data", "pos.db"));
            options.BackupDirectory = ResolveLocalPath(options.BackupDirectory, "backups");

            var dbDirectory = Path.GetDirectoryName(options.DatabaseFilePath);
            if (!string.IsNullOrWhiteSpace(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            Directory.CreateDirectory(options.BackupDirectory);
        });

        services.PostConfigure<ReceiptPrintingOptions>(options =>
        {
            options.SpoolDirectory = ResolveLocalPath(options.SpoolDirectory, Path.Combine("spool", "receipts"));
            Directory.CreateDirectory(options.SpoolDirectory);
        });

        services.AddDbContext<PosDbContext>((serviceProvider, dbOptions) =>
        {
            var backupOptions = serviceProvider.GetRequiredService<IOptions<BackupOptions>>().Value;
            dbOptions.UseSqlite($"Data Source={backupOptions.DatabaseFilePath}");
        });

        services.AddScoped<SeedDataInitializer>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<ICheckoutExecutionHook, NoOpCheckoutExecutionHook>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IBackupRestoreService, BackupRestoreService>();
        services.AddScoped<IReceiptPrintService, ReceiptPrintService>();

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IReceiptPrinterGateway, FileReceiptPrinterGateway>();
        services.AddHostedService<AutomaticBackupHostedService>();

        return services;
    }

    private static string ResolveLocalPath(string configuredPath, string fallbackRelativePath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return BuildAppDataPath(fallbackRelativePath);
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return BuildAppDataPath(configuredPath);
    }

    private static string BuildAppDataPath(string relativePath)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "POS", relativePath);
    }
}

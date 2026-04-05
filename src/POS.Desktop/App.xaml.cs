using System.Windows;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions;
using POS.Desktop.Services;
using POS.Desktop.ViewModels;
using POS.Infrastructure.Backup;
using POS.Infrastructure.Data;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Services;

namespace POS.Desktop;

public partial class App : System.Windows.Application
{
	private ServiceProvider? _serviceProvider;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var services = new ServiceCollection();
		ConfigureServices(services);

		_serviceProvider = services.BuildServiceProvider();
		EnsureSeedDataAndActiveUser();

		var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
		var mainWindow = new MainWindow(mainViewModel);

		MainWindow = mainWindow;
		mainWindow.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_serviceProvider?.Dispose();
		base.OnExit(e);
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var rootDirectory = Path.Combine(localAppData, "POS");
		var databasePath = ResolvePathFromEnvironment("POS_DESKTOP_DB_PATH", Path.Combine(rootDirectory, "data", "pos.db"));
		var backupDirectory = ResolvePathFromEnvironment("POS_DESKTOP_BACKUP_DIR", Path.Combine(rootDirectory, "backups"));
		var spoolDirectory = ResolvePathFromEnvironment("POS_DESKTOP_SPOOL_DIR", Path.Combine(rootDirectory, "spool", "receipts"));
		var databaseDirectory = Path.GetDirectoryName(databasePath) ?? Path.Combine(rootDirectory, "data");

		var backupRetentionDays = ResolveIntFromEnvironment("POS_BACKUP_RETENTION_DAYS", 30, 1, 3650);
		var backupIntervalMinutes = ResolveIntFromEnvironment("POS_BACKUP_INTERVAL_MINUTES", 240, 5, 1440);
		var receiptMaxRetryAttempts = ResolveIntFromEnvironment("POS_RECEIPT_MAX_RETRY_ATTEMPTS", 3, 1, 10);
		var receiptRetryDelayMilliseconds = ResolveIntFromEnvironment("POS_RECEIPT_RETRY_DELAY_MS", 120, 0, 60000);

		Directory.CreateDirectory(databaseDirectory);
		Directory.CreateDirectory(backupDirectory);
		Directory.CreateDirectory(spoolDirectory);

		services.AddLogging(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Information);
			builder.AddDebug();
		});

		services.AddDbContext<PosDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
		services.AddSingleton(
			Options.Create(
				new BackupOptions
				{
					DatabaseFilePath = databasePath,
					BackupDirectory = backupDirectory,
					RetentionDays = backupRetentionDays,
					AutomaticBackupIntervalMinutes = backupIntervalMinutes,
				}));
		services.AddSingleton(
			Options.Create(
				new ReceiptPrintingOptions
				{
					SpoolDirectory = spoolDirectory,
					MaxRetryAttempts = receiptMaxRetryAttempts,
					RetryDelayMilliseconds = receiptRetryDelayMilliseconds,
				}));

		services.AddScoped<SeedDataInitializer>();
		services.AddScoped<IAuditLogService, AuditLogService>();
		services.AddScoped<ICheckoutExecutionHook, NoOpCheckoutExecutionHook>();
		services.AddScoped<ICheckoutService, CheckoutService>();
		services.AddScoped<IBackupRestoreService, BackupRestoreService>();
		services.AddScoped<IReceiptPrintService, ReceiptPrintService>();
		services.AddSingleton<ISystemClock, SystemClock>();
		services.AddSingleton<IReceiptPrinterGateway, FileReceiptPrinterGateway>();
		services.AddSingleton<IAudioFeedbackService, AudioFeedbackService>();
		services.AddSingleton<IOperatorSessionContext, OperatorSessionContext>();

		services.AddScoped<ICashierTerminalService, CashierTerminalService>();
		services.AddScoped<IManagementService, ManagementService>();
		services.AddScoped<CheckoutViewModel>();
		services.AddScoped<InventoryManagementViewModel>();
		services.AddScoped<SupplierManagementViewModel>();
		services.AddScoped<CustomerManagementViewModel>();
		services.AddScoped<ReportingDashboardViewModel>();
		services.AddScoped<UserManagementViewModel>();
		services.AddScoped<BackupRestoreViewModel>();
		services.AddScoped<MainViewModel>();
	}

	private void EnsureSeedDataAndActiveUser()
	{
		if (_serviceProvider is null)
		{
			throw new InvalidOperationException("Service provider is not initialized.");
		}

		using var scope = _serviceProvider.CreateScope();
		var initializer = scope.ServiceProvider.GetRequiredService<SeedDataInitializer>();
		initializer.InitializeAsync().GetAwaiter().GetResult();

		var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
		var hasActiveUser = dbContext.Users.AsNoTracking().Any(x => x.IsActive);

		if (hasActiveUser)
		{
			return;
		}

		var message = "No active users were found. Set POS_ADMIN_USERNAME and POS_ADMIN_PASSWORD and then restart the application.";
		MessageBox.Show(message, "POS Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
		Shutdown(-1);
	}

	private static string ResolvePathFromEnvironment(string environmentKey, string fallbackPath)
	{
		var configuredValue = Environment.GetEnvironmentVariable(environmentKey);
		if (string.IsNullOrWhiteSpace(configuredValue))
		{
			return fallbackPath;
		}

		try
		{
			var expanded = Environment.ExpandEnvironmentVariables(configuredValue.Trim());
			return Path.GetFullPath(expanded);
		}
		catch
		{
			return fallbackPath;
		}
	}

	private static int ResolveIntFromEnvironment(string environmentKey, int fallbackValue, int minValue, int maxValue)
	{
		var configuredValue = Environment.GetEnvironmentVariable(environmentKey);
		if (!int.TryParse(configuredValue, out var parsed))
		{
			return fallbackValue;
		}

		return Math.Clamp(parsed, minValue, maxValue);
	}
}


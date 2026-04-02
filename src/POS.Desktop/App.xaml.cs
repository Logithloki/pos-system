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
		var databaseDirectory = Path.Combine(localAppData, "POS", "data");
		var databasePath = Path.Combine(databaseDirectory, "pos.db");
		var backupDirectory = Path.Combine(localAppData, "POS", "backups");
		var spoolDirectory = Path.Combine(localAppData, "POS", "spool", "receipts");

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
					RetentionDays = 30,
					AutomaticBackupIntervalMinutes = 240,
				}));
		services.AddSingleton(
			Options.Create(
				new ReceiptPrintingOptions
				{
					SpoolDirectory = spoolDirectory,
					MaxRetryAttempts = 3,
					RetryDelayMilliseconds = 120,
				}));

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
}


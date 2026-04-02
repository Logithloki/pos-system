using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class BackupRestoreViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _createBackupCommand;
    private readonly AsyncRelayCommand _restoreCommand;

    private BackupItem? _selectedBackup;
    private bool _confirmRestore;

    public BackupRestoreViewModel(IManagementService managementService)
    {
        _managementService = managementService;

        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        _createBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        _restoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedBackup is not null && ConfirmRestore);
    }

    public ObservableCollection<BackupItem> Backups { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand CreateBackupCommand => _createBackupCommand;

    public ICommand RestoreCommand => _restoreCommand;

    public BackupItem? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value))
            {
                _restoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ConfirmRestore
    {
        get => _confirmRestore;
        set
        {
            if (SetProperty(ref _confirmRestore, value))
            {
                _restoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var data = await _managementService.GetBackupsAsync(cancellationToken);

        Backups.Clear();
        foreach (var backup in data)
        {
            Backups.Add(backup);
        }

        ConfirmRestore = false;
        SetInfo($"Backups loaded: {Backups.Count}.");
    }

    public async Task CreateBackupAsync()
    {
        try
        {
            var backup = await _managementService.CreateBackupAsync();
            await RefreshAsync();
            SelectedBackup = Backups.FirstOrDefault(x => x.FilePath == backup.FilePath);
            SetInfo($"Backup created: {backup.FilePath}");
        }
        catch (Exception ex)
        {
            SetError($"Backup failed: {ex.Message}");
        }
    }

    public async Task RestoreAsync()
    {
        if (SelectedBackup is null)
        {
            return;
        }

        try
        {
            await _managementService.RestoreBackupAsync(SelectedBackup.FilePath);
            ConfirmRestore = false;
            await RefreshAsync();
            SetInfo("Restore completed. Reload data tabs to confirm state.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Restore failed: {ex.Message}");
        }
    }
}

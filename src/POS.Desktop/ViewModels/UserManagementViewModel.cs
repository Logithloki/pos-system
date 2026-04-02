using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class UserManagementViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _createCashierCommand;
    private readonly AsyncRelayCommand _resetPasswordCommand;
    private readonly AsyncRelayCommand _toggleStatusCommand;

    private UserAdminItem? _selectedUser;

    private string _newUsername = string.Empty;
    private string _newFullName = string.Empty;
    private string _newEmail = string.Empty;
    private string _newPassword = string.Empty;
    private string _resetPasswordInput = string.Empty;

    public UserManagementViewModel(IManagementService managementService)
    {
        _managementService = managementService;

        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        _createCashierCommand = new AsyncRelayCommand(CreateCashierAsync);
        _resetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => SelectedUser is not null);
        _toggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync, () => SelectedUser is not null);
    }

    public ObservableCollection<UserAdminItem> Users { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand CreateCashierCommand => _createCashierCommand;

    public ICommand ResetPasswordCommand => _resetPasswordCommand;

    public ICommand ToggleStatusCommand => _toggleStatusCommand;

    public UserAdminItem? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                _resetPasswordCommand.RaiseCanExecuteChanged();
                _toggleStatusCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public string NewFullName
    {
        get => _newFullName;
        set => SetProperty(ref _newFullName, value);
    }

    public string NewEmail
    {
        get => _newEmail;
        set => SetProperty(ref _newEmail, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ResetPasswordInput
    {
        get => _resetPasswordInput;
        set => SetProperty(ref _resetPasswordInput, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var users = await _managementService.GetUsersAsync(cancellationToken);

        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(user);
        }

        SetInfo($"Users loaded: {Users.Count}.");
    }

    public async Task CreateCashierAsync()
    {
        try
        {
            var created = await _managementService.CreateCashierAsync(
                new CreateCashierRequest
                {
                    Username = NewUsername,
                    FullName = NewFullName,
                    Password = NewPassword,
                    Email = string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail,
                });

            NewUsername = string.Empty;
            NewFullName = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;

            await RefreshAsync();
            SelectedUser = Users.FirstOrDefault(x => x.Id == created.Id);
            SetInfo($"Cashier {created.Username} created.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Create cashier failed: {ex.Message}");
        }
    }

    public async Task ResetPasswordAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            await _managementService.ResetUserPasswordAsync(SelectedUser.Id, ResetPasswordInput);
            ResetPasswordInput = string.Empty;
            SetInfo($"Password reset for {SelectedUser.Username}.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Reset failed: {ex.Message}");
        }
    }

    public async Task ToggleStatusAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        try
        {
            await _managementService.SetUserActiveAsync(SelectedUser.Id, !SelectedUser.IsActive);
            await RefreshAsync();
            SetInfo($"User {SelectedUser.Username} status updated.");
        }
        catch (Exception ex)
        {
            SetError($"Status update failed: {ex.Message}");
        }
    }
}

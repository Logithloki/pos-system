using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class CustomerManagementViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _toggleStatusCommand;
    private readonly RelayCommand _newCommand;

    private CustomerItem? _selectedCustomer;

    private string _name = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _loyaltyPointsInput = "0";

    public CustomerManagementViewModel(IManagementService managementService)
    {
        _managementService = managementService;

        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        _saveCommand = new AsyncRelayCommand(SaveAsync);
        _toggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync, () => SelectedCustomer is not null);
        _newCommand = new RelayCommand(NewCustomer);
    }

    public ObservableCollection<CustomerItem> Customers { get; } = new();

    public ObservableCollection<CustomerPurchaseItem> PurchaseHistory { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand ToggleStatusCommand => _toggleStatusCommand;

    public ICommand NewCommand => _newCommand;

    public CustomerItem? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                LoadFromSelected();
                _toggleStatusCommand.RaiseCanExecuteChanged();
                _ = LoadHistoryForSelectedAsync();
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string LoyaltyPointsInput
    {
        get => _loyaltyPointsInput;
        set => SetProperty(ref _loyaltyPointsInput, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _managementService.GetCustomersAsync(cancellationToken);

        Customers.Clear();
        foreach (var customer in customers)
        {
            Customers.Add(customer);
        }

        SetInfo($"Customers loaded: {Customers.Count}.");
    }

    public async Task SaveAsync()
    {
        try
        {
            if (!int.TryParse(LoyaltyPointsInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var points))
            {
                SetError("Loyalty points value is invalid.");
                return;
            }

            var saved = await _managementService.SaveCustomerAsync(
                new CustomerUpsertRequest
                {
                    Id = SelectedCustomer?.Id,
                    Name = Name,
                    Phone = Phone,
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
                    LoyaltyPoints = points,
                    IsActive = SelectedCustomer?.IsActive ?? true,
                });

            await RefreshAsync();
            SelectedCustomer = Customers.FirstOrDefault(x => x.Id == saved.Id);
            SetInfo($"Customer {saved.Name} saved.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Save failed: {ex.Message}");
        }
    }

    public async Task ToggleStatusAsync()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        try
        {
            await _managementService.SetCustomerActiveAsync(SelectedCustomer.Id, !SelectedCustomer.IsActive);
            await RefreshAsync();
            SetInfo($"Customer {SelectedCustomer.Name} status updated.");
        }
        catch (Exception ex)
        {
            SetError($"Status update failed: {ex.Message}");
        }
    }

    public void NewCustomer()
    {
        SelectedCustomer = null;
        Name = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        LoyaltyPointsInput = "0";
        PurchaseHistory.Clear();
        SetInfo("New customer form ready.");
    }

    private async Task LoadHistoryForSelectedAsync()
    {
        PurchaseHistory.Clear();

        if (SelectedCustomer is null)
        {
            return;
        }

        var history = await _managementService.GetCustomerPurchaseHistoryAsync(SelectedCustomer.Id);
        foreach (var record in history)
        {
            PurchaseHistory.Add(record);
        }
    }

    private void LoadFromSelected()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        Name = SelectedCustomer.Name;
        Phone = SelectedCustomer.Phone;
        Email = SelectedCustomer.Email ?? string.Empty;
        LoyaltyPointsInput = SelectedCustomer.LoyaltyPoints.ToString(CultureInfo.CurrentCulture);
    }
}

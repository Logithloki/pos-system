using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class SupplierManagementViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _toggleStatusCommand;
    private readonly RelayCommand _newCommand;

    private SupplierItem? _selectedSupplier;

    private string _name = string.Empty;
    private string _contact = string.Empty;
    private string _address = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;

    public SupplierManagementViewModel(IManagementService managementService)
    {
        _managementService = managementService;
        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        _saveCommand = new AsyncRelayCommand(SaveAsync);
        _toggleStatusCommand = new AsyncRelayCommand(ToggleStatusAsync, () => SelectedSupplier is not null);
        _newCommand = new RelayCommand(NewSupplier);
    }

    public ObservableCollection<SupplierItem> Suppliers { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand ToggleStatusCommand => _toggleStatusCommand;

    public ICommand NewCommand => _newCommand;

    public SupplierItem? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
            {
                LoadFromSelected();
                _toggleStatusCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Contact
    {
        get => _contact;
        set => SetProperty(ref _contact, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var data = await _managementService.GetSuppliersAsync(cancellationToken);

        Suppliers.Clear();
        foreach (var supplier in data)
        {
            Suppliers.Add(supplier);
        }

        SetInfo($"Suppliers loaded: {Suppliers.Count}.");
    }

    public async Task SaveAsync()
    {
        try
        {
            var saved = await _managementService.SaveSupplierAsync(
                new SupplierUpsertRequest
                {
                    Id = SelectedSupplier?.Id,
                    Name = Name,
                    Contact = Contact,
                    Address = Address,
                    Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone,
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
                    IsActive = SelectedSupplier?.IsActive ?? true,
                });

            await RefreshAsync();
            SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == saved.Id);
            SetInfo($"Supplier {saved.Name} saved.");
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
        if (SelectedSupplier is null)
        {
            return;
        }

        try
        {
            await _managementService.SetSupplierActiveAsync(SelectedSupplier.Id, !SelectedSupplier.IsActive);
            await RefreshAsync();
            SetInfo($"Supplier {SelectedSupplier.Name} status updated.");
        }
        catch (Exception ex)
        {
            SetError($"Status update failed: {ex.Message}");
        }
    }

    public void NewSupplier()
    {
        SelectedSupplier = null;
        Name = string.Empty;
        Contact = string.Empty;
        Address = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        SetInfo("New supplier form ready.");
    }

    private void LoadFromSelected()
    {
        if (SelectedSupplier is null)
        {
            return;
        }

        Name = SelectedSupplier.Name;
        Contact = SelectedSupplier.Contact;
        Address = SelectedSupplier.Address;
        Phone = SelectedSupplier.Phone ?? string.Empty;
        Email = SelectedSupplier.Email ?? string.Empty;
    }
}

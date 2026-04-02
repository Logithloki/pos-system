using POS.Desktop.Infrastructure;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly IOperatorSessionContext _operatorSessionContext;

    private int _selectedTabIndex;
    private bool _isAdminOperator;

    private bool _inventoryLoaded;
    private bool _suppliersLoaded;
    private bool _customersLoaded;
    private bool _reportsLoaded;
    private bool _usersLoaded;
    private bool _backupLoaded;

    public MainViewModel(
        CheckoutViewModel checkout,
        IOperatorSessionContext operatorSessionContext,
        InventoryManagementViewModel inventory,
        SupplierManagementViewModel suppliers,
        CustomerManagementViewModel customers,
        ReportingDashboardViewModel reporting,
        UserManagementViewModel users,
        BackupRestoreViewModel backupRestore)
    {
        _operatorSessionContext = operatorSessionContext;

        Checkout = checkout;
        Inventory = inventory;
        Suppliers = suppliers;
        Customers = customers;
        Reporting = reporting;
        Users = users;
        BackupRestore = backupRestore;
    }

    public CheckoutViewModel Checkout { get; }

    public InventoryManagementViewModel Inventory { get; }

    public SupplierManagementViewModel Suppliers { get; }

    public CustomerManagementViewModel Customers { get; }

    public ReportingDashboardViewModel Reporting { get; }

    public UserManagementViewModel Users { get; }

    public BackupRestoreViewModel BackupRestore { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool IsAdminOperator
    {
        get => _isAdminOperator;
        private set
        {
            if (SetProperty(ref _isAdminOperator, value))
            {
                RaisePropertyChanged(nameof(IsManagementTabsVisible));
            }
        }
    }

    public bool IsManagementTabsVisible => IsAdminOperator;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Checkout.InitializeAsync(cancellationToken);
        _operatorSessionContext.SetOperator(Checkout.OperatorUserId, Checkout.OperatorRole);

        IsAdminOperator = Checkout.IsAdminOperator;
        if (!IsAdminOperator)
        {
            SelectedTabIndex = 0;
        }

        Inventory.SetOperatorUserId(Checkout.OperatorUserId);
    }

    public async Task EnsureSelectedTabLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAdminOperator && SelectedTabIndex > 0)
        {
            SelectedTabIndex = 0;
            return;
        }

        switch (SelectedTabIndex)
        {
            case 1 when !_inventoryLoaded:
                await Inventory.InitializeAsync(cancellationToken);
                _inventoryLoaded = true;
                break;
            case 2 when !_suppliersLoaded:
                await Suppliers.InitializeAsync(cancellationToken);
                _suppliersLoaded = true;
                break;
            case 3 when !_customersLoaded:
                await Customers.InitializeAsync(cancellationToken);
                _customersLoaded = true;
                break;
            case 4 when !_reportsLoaded:
                await Reporting.InitializeAsync(cancellationToken);
                _reportsLoaded = true;
                break;
            case 5 when !_usersLoaded:
                await Users.InitializeAsync(cancellationToken);
                _usersLoaded = true;
                break;
            case 6 when !_backupLoaded:
                await BackupRestore.InitializeAsync(cancellationToken);
                _backupLoaded = true;
                break;
        }
    }
}

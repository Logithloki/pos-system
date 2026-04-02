using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class InventoryManagementViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _saveProductCommand;
    private readonly AsyncRelayCommand _toggleProductStatusCommand;
    private readonly AsyncRelayCommand _adjustStockCommand;
    private readonly RelayCommand _newProductCommand;

    private long _operatorUserId;
    private InventoryProductItem? _selectedProduct;
    private long? _selectedSupplierId;

    private string _barcode = string.Empty;
    private string _name = string.Empty;
    private string _priceInput = "0.00";
    private string _costInput = "0.00";
    private string _stockInput = "0";
    private string _reorderInput = "2";
    private string _adjustmentInput = "0";
    private string _adjustmentNote = string.Empty;

    public InventoryManagementViewModel(IManagementService managementService)
    {
        _managementService = managementService;

        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        _saveProductCommand = new AsyncRelayCommand(SaveProductAsync);
        _toggleProductStatusCommand = new AsyncRelayCommand(ToggleProductStatusAsync, () => SelectedProduct is not null);
        _adjustStockCommand = new AsyncRelayCommand(AdjustStockAsync, () => SelectedProduct is not null);
        _newProductCommand = new RelayCommand(NewProduct);
    }

    public ObservableCollection<InventoryProductItem> Products { get; } = new();

    public ObservableCollection<SupplierItem> Suppliers { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SaveProductCommand => _saveProductCommand;

    public ICommand ToggleProductStatusCommand => _toggleProductStatusCommand;

    public ICommand AdjustStockCommand => _adjustStockCommand;

    public ICommand NewProductCommand => _newProductCommand;

    public InventoryProductItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                LoadFromSelectedProduct();
                _toggleProductStatusCommand.RaiseCanExecuteChanged();
                _adjustStockCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string PriceInput
    {
        get => _priceInput;
        set => SetProperty(ref _priceInput, value);
    }

    public string CostInput
    {
        get => _costInput;
        set => SetProperty(ref _costInput, value);
    }

    public string StockInput
    {
        get => _stockInput;
        set => SetProperty(ref _stockInput, value);
    }

    public string ReorderInput
    {
        get => _reorderInput;
        set => SetProperty(ref _reorderInput, value);
    }

    public long? SelectedSupplierId
    {
        get => _selectedSupplierId;
        set => SetProperty(ref _selectedSupplierId, value);
    }

    public string AdjustmentInput
    {
        get => _adjustmentInput;
        set => SetProperty(ref _adjustmentInput, value);
    }

    public string AdjustmentNote
    {
        get => _adjustmentNote;
        set => SetProperty(ref _adjustmentNote, value);
    }

    public int LowStockCount => Products.Count(x => x.IsLowStock && x.IsActive);

    public void SetOperatorUserId(long userId)
    {
        _operatorUserId = userId;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var inventory = await _managementService.GetInventoryAsync(cancellationToken);
        var suppliers = await _managementService.GetSuppliersAsync(cancellationToken);

        Products.Clear();
        foreach (var item in inventory)
        {
            Products.Add(item);
        }

        Suppliers.Clear();
        foreach (var supplier in suppliers)
        {
            Suppliers.Add(supplier);
        }

        RaisePropertyChanged(nameof(LowStockCount));
        SetInfo($"Inventory loaded: {Products.Count} products ({LowStockCount} low stock).");
    }

    public async Task SaveProductAsync()
    {
        try
        {
            if (!decimal.TryParse(PriceInput, NumberStyles.Number, CultureInfo.CurrentCulture, out var price)
                && !decimal.TryParse(PriceInput, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            {
                SetError("Invalid price value.");
                return;
            }

            if (!decimal.TryParse(CostInput, NumberStyles.Number, CultureInfo.CurrentCulture, out var cost)
                && !decimal.TryParse(CostInput, NumberStyles.Number, CultureInfo.InvariantCulture, out cost))
            {
                SetError("Invalid cost value.");
                return;
            }

            if (!int.TryParse(StockInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var stock))
            {
                SetError("Invalid stock value.");
                return;
            }

            if (!int.TryParse(ReorderInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var reorder))
            {
                SetError("Invalid reorder level.");
                return;
            }

            var saved = await _managementService.SaveProductAsync(
                new ProductUpsertRequest
                {
                    Id = SelectedProduct?.Id,
                    Barcode = Barcode,
                    Name = Name,
                    Price = price,
                    Cost = cost,
                    QuantityOnHand = stock,
                    ReorderLevel = reorder,
                    SupplierId = SelectedSupplierId,
                    IsActive = SelectedProduct?.IsActive ?? true,
                },
                _operatorUserId);

            await RefreshAsync();
            SelectedProduct = Products.FirstOrDefault(x => x.Id == saved.Id);
            SetInfo($"Product {saved.Name} saved.");
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

    public async Task ToggleProductStatusAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        try
        {
            await _managementService.SetProductActiveAsync(SelectedProduct.Id, !SelectedProduct.IsActive);
            await RefreshAsync();
            SetInfo($"Product {SelectedProduct.Name} status updated.");
        }
        catch (Exception ex)
        {
            SetError($"Status update failed: {ex.Message}");
        }
    }

    public async Task AdjustStockAsync()
    {
        if (SelectedProduct is null)
        {
            SetError("Select a product first.");
            return;
        }

        if (!int.TryParse(AdjustmentInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var delta))
        {
            SetError("Invalid adjustment value.");
            return;
        }

        try
        {
            await _managementService.AdjustStockAsync(SelectedProduct.Id, delta, AdjustmentNote, _operatorUserId);
            AdjustmentInput = "0";
            AdjustmentNote = string.Empty;
            await RefreshAsync();
            SetInfo($"Stock adjusted by {delta} for {SelectedProduct.Name}.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Adjustment failed: {ex.Message}");
        }
    }

    public void NewProduct()
    {
        SelectedProduct = null;
        Barcode = string.Empty;
        Name = string.Empty;
        PriceInput = "0.00";
        CostInput = "0.00";
        StockInput = "0";
        ReorderInput = "2";
        SelectedSupplierId = null;
        SetInfo("New product form ready.");
    }

    private void LoadFromSelectedProduct()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        Barcode = SelectedProduct.Barcode;
        Name = SelectedProduct.Name;
        PriceInput = SelectedProduct.Price.ToString("0.00", CultureInfo.CurrentCulture);
        CostInput = SelectedProduct.Cost.ToString("0.00", CultureInfo.CurrentCulture);
        StockInput = SelectedProduct.QuantityOnHand.ToString(CultureInfo.CurrentCulture);
        ReorderInput = SelectedProduct.ReorderLevel.ToString(CultureInfo.CurrentCulture);
        SelectedSupplierId = SelectedProduct.SupplierId;
    }
}

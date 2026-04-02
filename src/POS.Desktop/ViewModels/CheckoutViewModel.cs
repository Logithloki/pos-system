using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Desktop.ViewModels;

public sealed class CheckoutViewModel : BindableBase
{
    private readonly ICashierTerminalService _terminalService;
    private readonly IAudioFeedbackService _audioFeedbackService;
    private readonly RelayCommand _addByBarcodeCommand;
    private readonly AsyncRelayCommand _payCommand;
    private readonly AsyncRelayCommand _quickAddCommand;
    private readonly RelayCommand _clearCartCommand;
    private readonly RelayCommand _cancelQuickAddCommand;
    private readonly RelayCommand _selectNextCartItemCommand;
    private readonly RelayCommand _selectPreviousCartItemCommand;
    private readonly RelayCommand _increaseQuantityCommand;
    private readonly RelayCommand _decreaseQuantityCommand;
    private readonly RelayCommand _removeSelectedItemCommand;
    private readonly RelayCommand _applyCashPresetCommand;

    private readonly Dictionary<string, CatalogProduct> _productsByBarcode = new(StringComparer.Ordinal);
    private readonly Dictionary<long, CartLineItem> _cartByProductId = new();

    private long _operatorUserId;
    private UserRole _operatorRole = UserRole.Cashier;
    private decimal _taxRatePercent = 5m;

    private string _operatorDisplayName = "Operator";
    private string _barcodeInput = string.Empty;
    private string _amountTenderedInput = "0.00";
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    private CartLineItem? _selectedCartItem;

    private string _inlineMessage = "Ready.";
    private bool _isInlineError;
    private string _performanceText = string.Empty;

    private decimal _subtotal;
    private decimal _taxAmount;
    private decimal _total;
    private decimal _changeDue;

    private bool _isQuickAddVisible;
    private string _quickAddBarcode = string.Empty;
    private string _quickAddName = string.Empty;
    private string _quickAddPriceInput = "0.00";
    private string _quickAddCostInput = "0.00";
    private string _quickAddStockInput = "1";
    private bool _autoPrintReceiptEnabled;

    public CheckoutViewModel(ICashierTerminalService terminalService, IAudioFeedbackService audioFeedbackService)
    {
        _terminalService = terminalService;
        _audioFeedbackService = audioFeedbackService;

        PaymentMethods = Enum.GetValues<PaymentMethod>();

        _addByBarcodeCommand = new RelayCommand(AddByBarcodeFromInput, CanAddByBarcode);
        _payCommand = new AsyncRelayCommand(PayAsync, CanPay);
        _quickAddCommand = new AsyncRelayCommand(QuickAddAsync, CanQuickAdd);
        _clearCartCommand = new RelayCommand(ClearCart);
        _cancelQuickAddCommand = new RelayCommand(CancelQuickAdd, () => IsQuickAddVisible);
        _selectNextCartItemCommand = new RelayCommand(SelectNextCartItem, () => CartItems.Count > 0);
        _selectPreviousCartItemCommand = new RelayCommand(SelectPreviousCartItem, () => CartItems.Count > 0);
        _increaseQuantityCommand = new RelayCommand(IncreaseSelectedQuantity, () => SelectedCartItem is not null);
        _decreaseQuantityCommand = new RelayCommand(DecreaseSelectedQuantity, () => SelectedCartItem is not null);
        _removeSelectedItemCommand = new RelayCommand(RemoveSelectedItem, () => SelectedCartItem is not null);
        _applyCashPresetCommand = new RelayCommand(ApplyCashPreset, _ => IsCashPayment && CartItems.Count > 0);
    }

    public event Action<string>? FocusRequested;

    public ObservableCollection<CartLineItem> CartItems { get; } = new();

    public IReadOnlyList<PaymentMethod> PaymentMethods { get; }

    public ICommand AddByBarcodeCommand => _addByBarcodeCommand;

    public ICommand PayCommand => _payCommand;

    public ICommand ClearCartCommand => _clearCartCommand;

    public ICommand QuickAddCommand => _quickAddCommand;

    public ICommand CancelQuickAddCommand => _cancelQuickAddCommand;

    public ICommand SelectNextCartItemCommand => _selectNextCartItemCommand;

    public ICommand SelectPreviousCartItemCommand => _selectPreviousCartItemCommand;

    public ICommand IncreaseQuantityCommand => _increaseQuantityCommand;

    public ICommand DecreaseQuantityCommand => _decreaseQuantityCommand;

    public ICommand RemoveSelectedItemCommand => _removeSelectedItemCommand;

    public ICommand ApplyCashPresetCommand => _applyCashPresetCommand;

    public string OperatorDisplayName
    {
        get => _operatorDisplayName;
        private set => SetProperty(ref _operatorDisplayName, value);
    }

    public long OperatorUserId => _operatorUserId;

    public UserRole OperatorRole
    {
        get => _operatorRole;
        private set
        {
            if (SetProperty(ref _operatorRole, value))
            {
                RaisePropertyChanged(nameof(IsAdminOperator));
            }
        }
    }

    public bool IsAdminOperator => OperatorRole == UserRole.Admin;

    public string BarcodeInput
    {
        get => _barcodeInput;
        set
        {
            if (SetProperty(ref _barcodeInput, value))
            {
                _addByBarcodeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AmountTenderedInput
    {
        get => _amountTenderedInput;
        set
        {
            if (SetProperty(ref _amountTenderedInput, value))
            {
                RecalculateTotals();
            }
        }
    }

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                RaisePropertyChanged(nameof(IsCashPayment));
                RaisePropertyChanged(nameof(AmountReceivedLabel));
                RecalculateTotals();
                RefreshCommandStates();
            }
        }
    }

    public CartLineItem? SelectedCartItem
    {
        get => _selectedCartItem;
        set
        {
            if (SetProperty(ref _selectedCartItem, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string InlineMessage
    {
        get => _inlineMessage;
        private set => SetProperty(ref _inlineMessage, value);
    }

    public bool IsInlineError
    {
        get => _isInlineError;
        private set => SetProperty(ref _isInlineError, value);
    }

    public string PerformanceText
    {
        get => _performanceText;
        private set => SetProperty(ref _performanceText, value);
    }

    public decimal Subtotal
    {
        get => _subtotal;
        private set => SetProperty(ref _subtotal, value);
    }

    public decimal TaxAmount
    {
        get => _taxAmount;
        private set => SetProperty(ref _taxAmount, value);
    }

    public decimal Total
    {
        get => _total;
        private set => SetProperty(ref _total, value);
    }

    public decimal ChangeDue
    {
        get => _changeDue;
        private set => SetProperty(ref _changeDue, value);
    }

    public bool AutoPrintReceiptEnabled
    {
        get => _autoPrintReceiptEnabled;
        set => SetProperty(ref _autoPrintReceiptEnabled, value);
    }

    public bool IsCashPayment => SelectedPaymentMethod == PaymentMethod.Cash;

    public string AmountReceivedLabel => IsCashPayment ? "Amount Received" : "Card Amount";

    public bool IsQuickAddVisible
    {
        get => _isQuickAddVisible;
        private set
        {
            if (SetProperty(ref _isQuickAddVisible, value))
            {
                _quickAddCommand.RaiseCanExecuteChanged();
                _cancelQuickAddCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuickAddBarcode
    {
        get => _quickAddBarcode;
        set => SetProperty(ref _quickAddBarcode, value);
    }

    public string QuickAddName
    {
        get => _quickAddName;
        set
        {
            if (SetProperty(ref _quickAddName, value))
            {
                _quickAddCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuickAddPriceInput
    {
        get => _quickAddPriceInput;
        set
        {
            if (SetProperty(ref _quickAddPriceInput, value))
            {
                _quickAddCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuickAddCostInput
    {
        get => _quickAddCostInput;
        set
        {
            if (SetProperty(ref _quickAddCostInput, value))
            {
                _quickAddCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string QuickAddStockInput
    {
        get => _quickAddStockInput;
        set
        {
            if (SetProperty(ref _quickAddStockInput, value))
            {
                _quickAddCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = await _terminalService.InitializeAsync(cancellationToken);

        _operatorUserId = bootstrap.OperatorUserId;
        OperatorDisplayName = bootstrap.OperatorDisplayName;
        OperatorRole = bootstrap.OperatorRole;
        _taxRatePercent = bootstrap.DefaultTaxRatePercent;

        _productsByBarcode.Clear();
        foreach (var product in bootstrap.Products)
        {
            _productsByBarcode[product.Barcode] = product;
        }

        ClearCart();
        SetInfo($"Ready. {_productsByBarcode.Count} products loaded.", focusBarcode: true);
    }

    public void ReportSystemError(string message)
    {
        SetError(message);
    }

    public void AddByBarcodeFromInput()
    {
        AddByBarcode(BarcodeInput);
    }

    public void AddByBarcode(string barcode)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var normalizedBarcode = barcode.Trim();
            if (string.IsNullOrWhiteSpace(normalizedBarcode))
            {
                SetError("Scan or enter a barcode.");
                return;
            }

            if (!_productsByBarcode.TryGetValue(normalizedBarcode, out var product))
            {
                QuickAddBarcode = normalizedBarcode;
                QuickAddName = string.Empty;
                QuickAddPriceInput = "0.00";
                QuickAddCostInput = "0.00";
                QuickAddStockInput = "1";
                IsQuickAddVisible = true;
                SetError($"Barcode {normalizedBarcode} not found. Quick add inline.", focusBarcode: false);
                FocusRequested?.Invoke("QuickAddName");
                return;
            }

            AddProductToCart(product, 1);
            BarcodeInput = string.Empty;

            stopwatch.Stop();
            SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Add item");
            _audioFeedbackService.PlayScanSuccess();
            SetInfo($"Added {product.Name}.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    public async Task QuickAddAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(QuickAddBarcode))
            {
                SetError("Quick add barcode is required.");
                return;
            }

            if (!TryParseMoney(QuickAddPriceInput, out var price))
            {
                SetError("Quick add price is invalid.");
                return;
            }

            if (!TryParseMoney(QuickAddCostInput, out var cost))
            {
                SetError("Quick add cost is invalid.");
                return;
            }

            if (!int.TryParse(QuickAddStockInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var stock))
            {
                SetError("Quick add stock is invalid.");
                return;
            }

            var created = await _terminalService.QuickAddProductAsync(
                new QuickAddProductRequest
                {
                    Barcode = QuickAddBarcode,
                    Name = QuickAddName,
                    Price = price,
                    Cost = cost,
                    QuantityOnHand = stock,
                });

            _productsByBarcode[created.Barcode] = created;
            AddProductToCart(created, 1);

            IsQuickAddVisible = false;
            BarcodeInput = string.Empty;

            stopwatch.Stop();
            SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Quick add");
            _audioFeedbackService.PlayScanSuccess();
            SetInfo($"Quick added {created.Name}.");
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Quick add failed: {ex.Message}");
        }
    }

    public async Task PayAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_operatorUserId <= 0)
            {
                SetError("Operator session is not initialized.");
                return;
            }

            if (CartItems.Count == 0)
            {
                SetError("Cart is empty. Scan an item before payment.");
                return;
            }

            var tendered = Total;
            if (IsCashPayment && !TryParseMoney(AmountTenderedInput, out tendered))
            {
                SetError("Tendered amount is invalid.");
                return;
            }

            var checkoutRequest = new CheckoutRequest
            {
                UserId = _operatorUserId,
                IdempotencyKey = $"desktop-{Guid.NewGuid():N}",
                TaxRatePercent = _taxRatePercent,
                PaymentMethod = SelectedPaymentMethod,
                AmountTendered = tendered,
                Items = CartItems
                    .Select(x => new CheckoutItemRequest { ProductId = x.ProductId, Quantity = x.Quantity })
                    .ToArray(),
            };

            var response = await _terminalService.CheckoutAsync(checkoutRequest);

            var printStatus = string.Empty;
            var printFailed = false;

            if (AutoPrintReceiptEnabled)
            {
                try
                {
                    await _terminalService.PrintReceiptAsync(response.ReceiptNumber);
                    printStatus = " | Receipt printed";
                }
                catch (Exception ex)
                {
                    printFailed = true;
                    printStatus = $" | Auto-print failed: {ex.Message}";
                }
            }

            ClearCartCore();

            stopwatch.Stop();
            SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Checkout");

            var message = $"Paid. Receipt {response.ReceiptNumber}. Change {response.ChangeDue:0.00}{printStatus}";

            if (printFailed)
            {
                SetError(message);
            }
            else
            {
                _audioFeedbackService.PlayPaymentSuccess();
                SetInfo(message);
            }
        }
        catch (AppValidationException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex)
        {
            SetError($"Checkout failed: {ex.Message}");
        }
    }

    public void ClearCart()
    {
        if (CartItems.Count == 0)
        {
            SetInfo("Cart already empty.");
            return;
        }

        ClearCartCore();
        SetInfo("Cart cleared.");
    }

    public void CancelQuickAdd()
    {
        IsQuickAddVisible = false;
        SetInfo("Quick add canceled.");
    }

    public void SelectNextCartItem()
    {
        if (CartItems.Count == 0)
        {
            return;
        }

        if (SelectedCartItem is null)
        {
            SelectedCartItem = CartItems[0];
            return;
        }

        var index = CartItems.IndexOf(SelectedCartItem);
        index = Math.Min(index + 1, CartItems.Count - 1);
        SelectedCartItem = CartItems[index];
    }

    public void SelectPreviousCartItem()
    {
        if (CartItems.Count == 0)
        {
            return;
        }

        if (SelectedCartItem is null)
        {
            SelectedCartItem = CartItems[0];
            return;
        }

        var index = CartItems.IndexOf(SelectedCartItem);
        index = Math.Max(index - 1, 0);
        SelectedCartItem = CartItems[index];
    }

    public void IncreaseSelectedQuantity()
    {
        if (SelectedCartItem is null)
        {
            SetError("Select an item to increase quantity.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        SelectedCartItem.Quantity += 1;

        RecalculateTotals();
        RefreshCommandStates();

        stopwatch.Stop();
        SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Qty +");
        SetInfo($"{SelectedCartItem.Name} quantity is now {SelectedCartItem.Quantity}.");
    }

    public void DecreaseSelectedQuantity()
    {
        if (SelectedCartItem is null)
        {
            SetError("Select an item to decrease quantity.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        if (SelectedCartItem.Quantity <= 1)
        {
            RemoveSelectedItem();
            return;
        }

        SelectedCartItem.Quantity -= 1;

        RecalculateTotals();
        RefreshCommandStates();

        stopwatch.Stop();
        SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Qty -");
        SetInfo($"{SelectedCartItem.Name} quantity is now {SelectedCartItem.Quantity}.");
    }

    public void RemoveSelectedItem()
    {
        if (SelectedCartItem is null)
        {
            SetError("Select an item to remove.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var removedItem = SelectedCartItem;

        CartItems.Remove(removedItem);
        _cartByProductId.Remove(removedItem.ProductId);
        SelectedCartItem = CartItems.Count > 0 ? CartItems[Math.Max(0, CartItems.Count - 1)] : null;

        RecalculateTotals();
        RefreshCommandStates();

        stopwatch.Stop();
        SetPerformance(stopwatch.Elapsed.TotalMilliseconds, "Remove item");
        SetInfo($"Removed {removedItem.Name}.");
    }

    public void ApplyCashPreset(object? parameter)
    {
        if (!IsCashPayment || CartItems.Count == 0)
        {
            return;
        }

        if (parameter is null)
        {
            return;
        }

        if (!decimal.TryParse(parameter.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var preset))
        {
            return;
        }

        if (preset < 0)
        {
            return;
        }

        var amount = Money.Round(Total + preset);
        AmountTenderedInput = amount.ToString("0.00", CultureInfo.CurrentCulture);
        SetInfo($"Amount received set to {amount:0.00}.");
    }

    private bool CanAddByBarcode()
    {
        return !string.IsNullOrWhiteSpace(BarcodeInput);
    }

    private bool CanPay()
    {
        return CartItems.Count > 0;
    }

    private bool CanQuickAdd()
    {
        return IsQuickAddVisible && !string.IsNullOrWhiteSpace(QuickAddName);
    }

    private void ClearCartCore()
    {
        CartItems.Clear();
        _cartByProductId.Clear();
        SelectedCartItem = null;
        AmountTenderedInput = "0.00";

        RecalculateTotals();
        RefreshCommandStates();
    }

    private void AddProductToCart(CatalogProduct product, int quantity)
    {
        if (_cartByProductId.TryGetValue(product.Id, out var existing))
        {
            existing.Quantity += quantity;
            SelectedCartItem = existing;
        }
        else
        {
            var line = new CartLineItem(product.Id, product.Barcode, product.Name, product.Price, quantity);
            CartItems.Add(line);
            _cartByProductId[product.Id] = line;
            SelectedCartItem = line;
        }

        RecalculateTotals();
        RefreshCommandStates();
    }

    private void RecalculateTotals()
    {
        var subtotal = 0m;
        for (var i = 0; i < CartItems.Count; i += 1)
        {
            subtotal += CartItems[i].LineTotal;
        }

        var tax = Money.Round(subtotal * (_taxRatePercent / 100m));
        var total = Money.Round(subtotal + tax);

        var tendered = 0m;
        decimal changeDue;

        if (IsCashPayment)
        {
            tendered = TryParseMoney(AmountTenderedInput, out var parsedTendered) ? parsedTendered : 0m;
            changeDue = Math.Max(0m, Money.Round(tendered - total));
        }
        else
        {
            tendered = total;
            changeDue = 0m;
            SetAmountTenderedSilently(tendered);
        }

        Subtotal = Money.Round(subtotal);
        TaxAmount = tax;
        Total = total;
        ChangeDue = changeDue;
    }

    private void SetAmountTenderedSilently(decimal amount)
    {
        var formatted = amount.ToString("0.00", CultureInfo.CurrentCulture);
        if (string.Equals(_amountTenderedInput, formatted, StringComparison.Ordinal))
        {
            return;
        }

        _amountTenderedInput = formatted;
        RaisePropertyChanged(nameof(AmountTenderedInput));
    }

    private static bool TryParseMoney(string input, out decimal value)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void SetInfo(string message, bool focusBarcode = true)
    {
        InlineMessage = message;
        IsInlineError = false;

        if (focusBarcode)
        {
            FocusRequested?.Invoke("Barcode");
        }
    }

    private void SetError(string message, bool focusBarcode = true)
    {
        InlineMessage = message;
        IsInlineError = true;
        _audioFeedbackService.PlayError();

        if (focusBarcode)
        {
            FocusRequested?.Invoke("Barcode");
        }
    }

    private void SetPerformance(double elapsedMilliseconds, string operation)
    {
        PerformanceText = $"{operation}: {elapsedMilliseconds:0.0} ms";
    }

    private void RefreshCommandStates()
    {
        _addByBarcodeCommand.RaiseCanExecuteChanged();
        _payCommand.RaiseCanExecuteChanged();
        _quickAddCommand.RaiseCanExecuteChanged();
        _clearCartCommand.RaiseCanExecuteChanged();
        _cancelQuickAddCommand.RaiseCanExecuteChanged();
        _selectNextCartItemCommand.RaiseCanExecuteChanged();
        _selectPreviousCartItemCommand.RaiseCanExecuteChanged();
        _increaseQuantityCommand.RaiseCanExecuteChanged();
        _decreaseQuantityCommand.RaiseCanExecuteChanged();
        _removeSelectedItemCommand.RaiseCanExecuteChanged();
        _applyCashPresetCommand.RaiseCanExecuteChanged();
    }
}

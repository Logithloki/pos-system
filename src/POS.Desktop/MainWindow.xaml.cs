using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Desktop.ViewModels;

namespace POS.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Checkout.FocusRequested += OnFocusRequested;

        try
        {
            await _viewModel.InitializeAsync();
            await _viewModel.EnsureSelectedTabLoadedAsync();
            BarcodeInputBox.Focus();
            BarcodeInputBox.SelectAll();
        }
        catch (Exception ex)
        {
            _viewModel.Checkout.ReportSystemError($"Startup failed: {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Checkout.FocusRequested -= OnFocusRequested;
    }

    private void OnFocusRequested(string target)
    {
        if (string.Equals(target, "Barcode", StringComparison.Ordinal))
        {
            BarcodeInputBox.Focus();
            BarcodeInputBox.SelectAll();
            return;
        }

        if (string.Equals(target, "QuickAddName", StringComparison.Ordinal))
        {
            QuickAddNameBox.Focus();
            QuickAddNameBox.SelectAll();
            return;
        }

        if (string.Equals(target, "Cart", StringComparison.Ordinal))
        {
            CartGrid.Focus();
        }
    }

    private void BarcodeInputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.Checkout.AddByBarcodeCommand.CanExecute(null))
            {
                _viewModel.Checkout.AddByBarcodeCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            CartGrid.Focus();
            e.Handled = true;
        }
    }

    private void QuickAddBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.Checkout.QuickAddCommand.CanExecute(null))
            {
                _viewModel.Checkout.QuickAddCommand.Execute(null);
            }

            e.Handled = true;
        }
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.SelectedTabIndex != 0)
        {
            return;
        }

        if (Keyboard.FocusedElement is TextBox textBox && textBox == QuickAddNameBox)
        {
            return;
        }

        if (Keyboard.FocusedElement is TextBox focusedTextBox && focusedTextBox != BarcodeInputBox && focusedTextBox != AmountReceivedInputBox)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            ExecuteCommand(_viewModel.Checkout.RemoveSelectedItemCommand);
            e.Handled = true;
            return;
        }

        var isPlus = e.Key == Key.Add || (e.Key == Key.OemPlus && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        if (isPlus)
        {
            ExecuteCommand(_viewModel.Checkout.IncreaseQuantityCommand);
            e.Handled = true;
            return;
        }

        var isMinus = e.Key == Key.Subtract || e.Key == Key.OemMinus;
        if (isMinus)
        {
            ExecuteCommand(_viewModel.Checkout.DecreaseQuantityCommand);
            e.Handled = true;
        }
    }

    private async void MainTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || !ReferenceEquals(sender, e.Source))
        {
            return;
        }

        try
        {
            await _viewModel.EnsureSelectedTabLoadedAsync();
        }
        catch (Exception ex)
        {
            _viewModel.Checkout.ReportSystemError($"Failed to load selected tab: {ex.Message}");
        }
    }

    private static void ExecuteCommand(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
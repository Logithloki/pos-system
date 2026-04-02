using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Desktop.Infrastructure;
using POS.Desktop.Models;
using POS.Desktop.Services;

namespace POS.Desktop.ViewModels;

public sealed class ReportingDashboardViewModel : ManagementSectionViewModelBase
{
    private readonly IManagementService _managementService;
    private readonly AsyncRelayCommand _refreshCommand;

    private ReportPeriod _selectedPeriod = ReportPeriod.Daily;
    private DateTime _fromUtc;
    private DateTime _toUtc;
    private decimal _totalSales;
    private decimal _totalProfit;
    private int _transactionCount;
    private decimal _averageSale;

    public ReportingDashboardViewModel(IManagementService managementService)
    {
        _managementService = managementService;
        _refreshCommand = new AsyncRelayCommand(() => RefreshAsync());

        AvailablePeriods = Enum.GetValues<ReportPeriod>();
    }

    public ObservableCollection<TopProductItem> TopProducts { get; } = new();

    public IReadOnlyList<ReportPeriod> AvailablePeriods { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public ReportPeriod SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public DateTime FromUtc
    {
        get => _fromUtc;
        private set => SetProperty(ref _fromUtc, value);
    }

    public DateTime ToUtc
    {
        get => _toUtc;
        private set => SetProperty(ref _toUtc, value);
    }

    public decimal TotalSales
    {
        get => _totalSales;
        private set => SetProperty(ref _totalSales, value);
    }

    public decimal TotalProfit
    {
        get => _totalProfit;
        private set => SetProperty(ref _totalProfit, value);
    }

    public int TransactionCount
    {
        get => _transactionCount;
        private set => SetProperty(ref _transactionCount, value);
    }

    public decimal AverageSale
    {
        get => _averageSale;
        private set => SetProperty(ref _averageSale, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var summary = await _managementService.GetReportSummaryAsync(SelectedPeriod, cancellationToken);

        FromUtc = summary.FromUtc;
        ToUtc = summary.ToUtc;
        TotalSales = summary.TotalSales;
        TotalProfit = summary.TotalProfit;
        TransactionCount = summary.TransactionCount;
        AverageSale = summary.AverageSale;

        TopProducts.Clear();
        foreach (var item in summary.TopProducts)
        {
            TopProducts.Add(item);
        }

        SetInfo($"{SelectedPeriod} report refreshed.");
    }
}
